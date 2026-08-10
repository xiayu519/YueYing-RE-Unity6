using System;
using System.Collections.Generic;
using System.IO;
using Jxqy.Domain.Content;
using Jxqy.Domain.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UIFont = UnityEngine.Font;
using UIImage = UnityEngine.UI.Image;
using UIText = UnityEngine.UI.Text;

namespace Jxqy.Editor.UI
{
    [InitializeOnLoad]
    public static class JxqyUiPrefabBuilder
    {
        public const string OutputDirectory =
            "Assets/AssetRaw/Jxqy/UI/Prefabs";
        public const string NativeFontAssetPath =
            "Assets/AssetRaw/Jxqy/UI/Fonts/" +
            "FZZhengYuan.ttf";
        public const string DialogueFontAssetPath =
            "Assets/AssetRaw/Jxqy/UI/Fonts/" +
            "FZZhunYuan.ttf";
        private const string UiAnimationRoot =
            "Assets/AssetRaw/Jxqy/Animations/asf/ui";
        private const float LogicalWidth = JxqyLogicalViewport.OriginalWidth;
        private const float LogicalHeight = JxqyLogicalViewport.OriginalHeight;
        private const float LeftPanel = LogicalWidth / 2f - 319f;
        private const float RightPanel = LogicalWidth / 2f;
        private const float TopPanelLeft = (LogicalWidth - 285f) / 2f;
        private const float ColumnPanelLeft = LogicalWidth / 2f - 320f;
        private const float ColumnPanelTop = LogicalHeight - 76f;
        private const float BottomPanelLeft =
            (LogicalWidth - 422f) / 2f + 102f;
        private const float BottomPanelTop = LogicalHeight - 70f;
        private const float DialoguePanelLeft =
            (LogicalWidth - 438f) / 2f;
        private const float DialoguePanelTop = LogicalHeight - 208f;
        private const float SaveLoadPanelLeft =
            (LogicalWidth - 640f) / 2f;
        private const float SaveLoadPanelTop =
            (LogicalHeight - 480f) / 2f;

        private static readonly string[] WindowNames =
        {
            "JxqyTitleUI",
            "JxqyFadeUI",
            "JxqyNoticeUI",
            "JxqyHudUI",
            "JxqyPartnerHeadsUI",
            "JxqyLittleMapUI",
            "JxqyTargetLifeUI",
            "JxqyTimerUI",
            "JxqyMessageUI",
            "JxqySystemMessageUI",
            "JxqyCombatFloatTextView",
            "JxqyDialogueUI",
            "JxqySelectionUI",
            "JxqyStatusUI",
            "JxqyMemoUI",
            "JxqyInventoryUI",
            "JxqyItemDetailUI",
            "JxqyEquipmentUI",
            "JxqyTrainingUI",
            "JxqySkillsUI",
            "JxqyMagicDetailUI",
            "JxqyTradeUI",
            "JxqyTradeGoodsUI",
            "JxqyMenuUI",
            "JxqySaveLoadUI",
        };
        private static UIFont _originalFont;
        private static UIFont _dialogueFont;

        private static readonly string RequestPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "Temp",
            "JxqyValidation",
            "build-ui-prefabs.request");
        private static readonly string ResultPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "Temp",
            "JxqyValidation",
            "build-ui-prefabs.result");

        static JxqyUiPrefabBuilder()
        {
            EditorApplication.update += PollRequest;
        }

        [MenuItem("TEngine/Jxqy/Build UI Prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(OutputDirectory);
            _originalFont = AssetDatabase.LoadAssetAtPath<UIFont>(
                NativeFontAssetPath);
            if (_originalFont == null)
                throw new FileNotFoundException(
                    $"Jxqy native UI font is missing: " +
                    $"{NativeFontAssetPath}");
            _dialogueFont = BuildDialogueFont();
            if (!PrefabExists("JxqyTitleUI"))
                BuildTitle();
            BuildFade();
            BuildNotice();
            BuildHud();
            BuildPartnerHeads();
            BuildLittleMap();
            BuildTargetLife();
            BuildTimer();
            BuildMessage();
            BuildSystemMessage();
            BuildCombatFloatTextView();
            BuildDialogue();
            BuildSelection();
            BuildStatus();
            BuildMemo();
            BuildInventory();
            BuildItemDetail();
            BuildEquipment();
            BuildTraining();
            BuildSkills();
            BuildMagicDetail();
            BuildTrade();
            BuildTradeGoods();
            if (!PrefabExists("JxqyMenuUI"))
                BuildMenu();
            BuildSaveLoad();
            ReplacePrefabFonts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Jxqy original-layout TEngine UI prefabs generated: " +
                $"{WindowNames.Length} -> {OutputDirectory}");
        }

        public static void BuildAllForCommandLine()
        {
            BuildAll();
        }

        [MenuItem("TEngine/Jxqy/Build Runtime-Owned UI Prefabs")]
        public static void BuildRuntimeOwnedUiPrefabs()
        {
            Directory.CreateDirectory(OutputDirectory);
            _originalFont = AssetDatabase.LoadAssetAtPath<UIFont>(
                NativeFontAssetPath);
            if (_originalFont == null)
                throw new FileNotFoundException(
                    $"Jxqy native UI font is missing: " +
                    $"{NativeFontAssetPath}");
            BuildPartnerHeads();
            BuildLittleMap();
            BuildTargetLife();
            BuildTimer();
            BuildMessage();
            BuildSystemMessage();
            BuildSelection();

            AddCooldownNodesToExistingSlotPrefabs();
            EnsureInventoryScrollTrack();
            EnsureTradeScrollTracks();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("TEngine/Jxqy/Refresh UI Fonts")]
        public static void RefreshFontsOnly()
        {
            UIFont nativeFont = ImportAndRequireFont(NativeFontAssetPath);
            UIFont dialogueFont = ImportAndRequireFont(
                DialogueFontAssetPath);
            int prefabCount = 0;
            int textCount = 0;
            var invalid = new List<string>();
            string nativeGuid = AssetDatabase.AssetPathToGUID(
                NativeFontAssetPath);
            string dialogueGuid = AssetDatabase.AssetPathToGUID(
                DialogueFontAssetPath);
            string projectRoot = Path.GetDirectoryName(
                Application.dataPath) ?? string.Empty;
            foreach (string prefabGuid in AssetDatabase.FindAssets(
                         "t:Prefab",
                         new[] { "Assets" }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(
                    prefabGuid);
                string absolutePath = Path.GetFullPath(Path.Combine(
                    projectRoot,
                    prefabPath));
                string serialized = File.ReadAllText(absolutePath);
                if (!serialized.Contains(nativeGuid,
                        StringComparison.Ordinal) &&
                    !serialized.Contains(dialogueGuid,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(
                    prefabPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
                if (prefab == null)
                {
                    invalid.Add($"{prefabPath}: prefab failed to load");
                    continue;
                }

                prefabCount++;
                foreach (UIText text in
                         prefab.GetComponentsInChildren<UIText>(true))
                {
                    textCount++;
                    if (text.font == null)
                    {
                        invalid.Add(
                            $"{prefabPath}/{text.name}: missing font");
                    }
                    else if (text.font != nativeFont &&
                             text.font != dialogueFont)
                    {
                        invalid.Add(
                            $"{prefabPath}/{text.name}: unexpected font " +
                            text.font.name);
                    }
                }
            }

            if (textCount == 0 || invalid.Count > 0)
            {
                throw new InvalidDataException(
                    $"Jxqy UI font validation failed. Prefabs={prefabCount}, " +
                    $"Texts={textCount}, Invalid={invalid.Count}. " +
                    string.Join(" | ", invalid.ToArray()));
            }
            Debug.Log(
                $"Jxqy UI fonts validated: Prefabs={prefabCount}, " +
                $"Texts={textCount}, Missing=0; {NativeFontAssetPath}, " +
                DialogueFontAssetPath);
        }

        [MenuItem("TEngine/Jxqy/Use Built-in UI Text Outlines")]
        public static void EnsureBuiltInTextOutlines()
        {
            int outlineCount = 0;
            foreach (string prefabGuid in AssetDatabase.FindAssets(
                         "t:Prefab",
                         new[] { OutputDirectory }))
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                bool changed = false;
                try
                {
                    foreach (UIText text in
                             root.GetComponentsInChildren<UIText>(true))
                    {
                        if (text.name != "m_text_Detail" &&
                            text.name != "m_text_Value")
                        {
                            continue;
                        }

                        Outline outline = text.GetComponent<Outline>();
                        if (outline == null)
                        {
                            outline = text.gameObject.AddComponent<Outline>();
                            changed = true;
                        }
                        if (!outline.enabled ||
                            outline.effectColor !=
                            new Color(0f, 0f, 0f, 0.95f) ||
                            outline.effectDistance != new Vector2(1f, -1f) ||
                            !outline.useGraphicAlpha)
                        {
                            outline.enabled = true;
                            outline.effectColor =
                                new Color(0f, 0f, 0f, 0.95f);
                            outline.effectDistance = new Vector2(1f, -1f);
                            outline.useGraphicAlpha = true;
                            changed = true;
                        }

                        outlineCount++;
                    }

                    if (changed)
                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Jxqy built-in UI text outlines ensured: {outlineCount}");
        }

        private static UIFont ImportAndRequireFont(string assetPath)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            UIFont font = AssetDatabase.LoadAssetAtPath<UIFont>(assetPath);
            if (font == null)
            {
                throw new FileNotFoundException(
                    $"Jxqy Unity UI font failed to import: {assetPath}");
            }
            return font;
        }

        private static bool PrefabExists(string prefabName)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{OutputDirectory}/{prefabName}.prefab") != null;
        }

        private static void BuildStandaloneRuntimeWindow(string name)
        {
            if (PrefabExists(name))
                return;
            GameObject root = CreateCanvas(name);
            UIText fontSource = CreateText(
                "m_text_FontSource",
                root.transform,
                0f,
                0f,
                1f,
                1f,
                12,
                TextAnchor.UpperLeft,
                Color.clear);
            fontSource.raycastTarget = false;
            Save(root);
        }

        private static void BuildFade()
        {
            GameObject root = CreateCanvas("JxqyFadeUI");
            UIImage overlay = CreateImage(
                "m_image_Overlay",
                root.transform,
                0f,
                0f,
                LogicalWidth,
                LogicalHeight,
                Color.clear);
            overlay.raycastTarget = true;
            Save(root);
        }

        private static void BuildNotice()
        {
            GameObject root = CreateCanvas("JxqyNoticeUI");
            CreateLegacyNotice(root.transform);
            Save(root);
        }

        private static void PollRequest()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
                return;
            if (!File.Exists(RequestPath))
                return;
            string request;
            try
            {
                request = File.ReadAllText(RequestPath).Trim();
                File.Delete(RequestPath);
            }
            catch (IOException)
            {
                // The request is written atomically by an external process;
                // retry on the next editor update if its file handle is still
                // being released.
                return;
            }
            try
            {
                if (string.Equals(
                        request,
                        "fonts-only",
                        StringComparison.OrdinalIgnoreCase))
                {
                    RefreshFontsOnly();
                }
                else if (string.Equals(
                        request,
                        "runtime-owned-ui",
                        StringComparison.OrdinalIgnoreCase))
                {
                    BuildRuntimeOwnedUiPrefabs();
                }
                else if (string.Equals(
                        request,
                        "outlines-only",
                        StringComparison.OrdinalIgnoreCase))
                {
                    EnsureBuiltInTextOutlines();
                }
                else
                {
                    BuildAll();
                }
                Directory.CreateDirectory(
                    Path.GetDirectoryName(ResultPath) ?? string.Empty);
                File.WriteAllText(
                    ResultPath,
                    $"Passed|{WindowNames.Length}|{DateTime.UtcNow:O}");
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, $"Failed|{exception}");
                throw;
            }
        }

        private static void BuildTitle()
        {
            GameObject root = CreateCanvas("JxqyTitleUI");
            GameObject background =
                CreateUiObject("m_raw_Background", root.transform);
            Stretch(background.GetComponent<RectTransform>());
            RawImage backgroundImage = background.AddComponent<RawImage>();
            backgroundImage.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/AssetRaw/Jxqy/Images/asf/ui/title/title.jpg");
            backgroundImage.raycastTarget = false;

            CreateAsfButton(
                root.transform,
                "m_btn_NewGame",
                "title/InitBtn.asf",
                327,
                112,
                81,
                66);
            CreateAsfButton(
                root.transform,
                "m_btn_LoadGame",
                "title/LoadBtn.asf",
                327,
                177,
                81,
                66);
            CreateAsfButton(
                root.transform,
                "m_btn_Credits",
                "title/TeamBtn.asf",
                327,
                240,
                81,
                66);
            CreateAsfButton(
                root.transform,
                "m_btn_Exit",
                "title/ExitBtn.asf",
                327,
                303,
                81,
                66);
            Save(root);
        }

        private static void BuildHud()
        {
            GameObject root = CreateCanvas("JxqyHudUI");
            CreateAsfImage(
                root.transform,
                "m_raw_TopPanel",
                "top/window.asf",
                TopPanelLeft,
                0);
            string[] topAssets =
            {
                "top/BtnState.asf",
                "top/BtnEquip.asf",
                "top/BtnXiuLian.asf",
                "top/BtnGoods.asf",
                "top/BtnMagic.asf",
                "top/BtnNotes.asf",
                "top/BtnOption.asf",
            };
            string[] topNames =
            {
                "m_btn_Status",
                "m_btn_Equipment",
                "m_btn_Training",
                "m_btn_Inventory",
                "m_btn_Skills",
                "m_btn_Memo",
                "m_btn_Menu",
            };
            float[] topLeft = { 52, 80, 107, 135, 162, 189, 216 };
            for (int index = 0; index < topNames.Length; index++)
            {
                CreateAsfButton(
                    root.transform,
                    topNames[index],
                    topAssets[index],
                    TopPanelLeft + topLeft[index],
                    0,
                    19,
                    19);
            }

            CreateAsfImage(
                root.transform,
                "m_raw_StatePanel",
                "column/panel9.asf",
                ColumnPanelLeft,
                ColumnPanelTop);
            CreateMeter(
                root.transform,
                "m_raw_Life",
                "column/ColLife.asf",
                ColumnPanelLeft + 11f,
                ColumnPanelTop + 22f);
            CreateMeter(
                root.transform,
                "m_raw_Thew",
                "column/ColThew.asf",
                ColumnPanelLeft + 59f,
                ColumnPanelTop + 22f);
            CreateMeter(
                root.transform,
                "m_raw_Mana",
                "column/ColMana.asf",
                ColumnPanelLeft + 113f,
                ColumnPanelTop + 22f);
            CreateAsfImage(
                root.transform,
                "m_raw_ShortcutPanel",
                "bottom/window.asf",
                BottomPanelLeft,
                BottomPanelTop);
            float[] shortcutLeft =
            {
                7, 44, 82, 199, 238, 277, 316, 354,
            };
            for (int index = 0; index < shortcutLeft.Length; index++)
            {
                CreateSlot(
                    root.transform,
                    $"m_item_Shortcut{index + 1}",
                    BottomPanelLeft + shortcutLeft[index],
                    BottomPanelTop + 20,
                    30,
                    40,
                    10);
            }
            Save(root);
        }

        private static void BuildLittleMap()
        {
            GameObject root = CreateCanvas("JxqyLittleMapUI");
            GameObject group = CreateUiObject(
                "m_group_LittleMap", root.transform);
            SetTopLeft(
                group.GetComponent<RectTransform>(),
                (LogicalWidth - 640f) / 2f,
                0f,
                640f,
                445f);
            RawImage panel = CreateAsfImage(
                group.transform,
                "m_raw_Panel",
                "littlemap/panel.asf",
                0f,
                0f);
            panel.raycastTarget = true;

            // Keep a prefab-owned font source for the common UIWindow
            // binding/validation path. It is metadata, not a visible label.
            UnityEngine.UI.Text fontSource = CreateText(
                "m_text_FontSource",
                group.transform,
                0f,
                0f,
                1f,
                1f,
                1,
                TextAnchor.UpperLeft,
                Color.clear);
            fontSource.raycastTarget = false;
            fontSource.gameObject.SetActive(false);

            RawImage map = CreateTransparentRawImage(
                "m_raw_Map",
                group.transform,
                160f,
                120f,
                320f,
                240f);
            map.raycastTarget = true;
            Mask mask = map.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            AddRuntimeComponent(
                map.gameObject,
                "GameLogic.JxqyPointerClickRelay, GameLogic");

            CreateText(
                "m_text_MapName",
                group.transform,
                210f,
                92f,
                220f,
                30f,
                12,
                TextAnchor.MiddleCenter,
                new Color32(76, 56, 48, 204));
            CreateText(
                "m_text_MapTip",
                group.transform,
                160f,
                370f,
                260f,
                30f,
                12,
                TextAnchor.MiddleLeft,
                new Color32(76, 56, 48, 204));

            CreateLittleMapButton(
                group.transform, "Left", "btnleft.asf", 437f, 379f, true);
            CreateLittleMapButton(
                group.transform, "Right", "btnright.asf", 464f, 379f, true);
            CreateLittleMapButton(
                group.transform, "Up", "btnup.asf", 448f, 368f, true);
            CreateLittleMapButton(
                group.transform, "Down", "btndown.asf", 448f, 395f, true);
            CreateLittleMapButton(
                group.transform, "Close", "btnclose.asf", 448f, 379f, false);

            for (int index = 0; index < 4; index++)
            {
                RawImage source = CreateTransparentRawImage(
                    $"m_raw_MarkerSource{index}",
                    group.transform,
                    0f,
                    0f,
                    8f,
                    8f);
                source.enabled = false;
            }
            RawImage markerTemplate = CreateTransparentRawImage(
                "m_raw_MarkerTemplate",
                map.transform,
                0f,
                0f,
                8f,
                8f);
            markerTemplate.raycastTarget = false;
            markerTemplate.gameObject.SetActive(false);
            Save(root);
        }

        private static void BuildPartnerHeads()
        {
            GameObject root = CreateCanvas("JxqyPartnerHeadsUI");
            RawImage template = CreateTransparentRawImage(
                "m_item_PartnerHeadTemplate",
                root.transform,
                5f,
                5f,
                36f,
                36f);
            template.raycastTarget = true;
            AddRuntimeComponent(
                template.gameObject,
                "GameLogic.JxqyPointerClickRelay, GameLogic");
            CreateText(
                "m_text_Level",
                template.transform,
                39f,
                27f,
                30f,
                10f,
                7,
                TextAnchor.UpperLeft,
                Color.white);
            template.gameObject.SetActive(false);
            Save(root);
        }

        private static void BuildTargetLife()
        {
            GameObject root = CreateCanvas("JxqyTargetLifeUI");
            UIImage group = CreateImage(
                "m_group_TargetLife",
                root.transform,
                250f,
                50f,
                300f,
                25f,
                new Color(0f, 0f, 0f, 0.7f));
            UIImage fill = CreateImageStretch(
                "m_img_TargetLife",
                group.transform,
                new Color32(147, 16, 19, 230));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            CreateTextStretch(
                "m_text_TargetLife",
                group.transform,
                14,
                TextAnchor.MiddleCenter,
                Color.white,
                Vector2.zero,
                Vector2.zero);
            group.gameObject.SetActive(false);
            Save(root);
        }

        private static void BuildTimer()
        {
            GameObject root = CreateCanvas("JxqyTimerUI");
            RawImage group = CreateAsfImage(
                root.transform,
                "m_group_Timer",
                "timer/window.asf",
                503f,
                0f);
            CreateText(
                "m_text_Timer",
                group.transform,
                74f,
                44f,
                120f,
                22f,
                20,
                TextAnchor.UpperLeft,
                new Color32(255, 0, 0, 204));
            group.gameObject.SetActive(false);
            Save(root);
        }

        private static void BuildMessage()
        {
            GameObject root = CreateCanvas("JxqyMessageUI");
            RawImage group = CreateAsfImage(
                root.transform,
                "m_group_Message",
                "message/msgbox.asf",
                272f,
                459f);
            group.raycastTarget = true;
            AddRuntimeComponent(
                group.gameObject,
                "GameLogic.JxqyPointerClickRelay, GameLogic");
            CreateText(
                "m_text_Message",
                group.transform,
                46f,
                32f,
                148f,
                50f,
                12,
                TextAnchor.UpperLeft,
                new Color32(155, 34, 22, 204));
            group.gameObject.SetActive(false);
            Save(root);
        }

        private static void BuildSystemMessage()
        {
            GameObject root = CreateCanvas("JxqySystemMessageUI");
            CreateText(
                "m_text_SystemMessages",
                root.transform,
                50f,
                250f,
                750f,
                300f,
                10,
                TextAnchor.LowerLeft,
                Color.white);
            Save(root);
        }

        private static void BuildSelection()
        {
            GameObject root = CreateCanvas("JxqySelectionUI");
            UIImage group = CreateImageStretch(
                "m_group_Selection",
                root.transform,
                new Color(0f, 0f, 0f, 0.8f));
            Stretch(root.GetComponent<RectTransform>());
            group.raycastTarget = true;
            UIText message = CreateText(
                "m_text_Message",
                group.transform,
                0f,
                60f,
                0f,
                60f,
                18,
                TextAnchor.UpperCenter,
                new Color(1f, 215f / 255f, 0f, 0.8f));
            message.rectTransform.anchorMax = new Vector2(1f, 1f);
            message.rectTransform.sizeDelta = new Vector2(0f, 60f);
            GameObject choice = CreateUiObject(
                "m_item_ChoiceTemplate", group.transform);
            RectTransform choiceRect = choice.GetComponent<RectTransform>();
            choiceRect.anchorMin = new Vector2(0.0625f, 0.8f);
            choiceRect.anchorMax = new Vector2(0.9375f, 0.8f);
            choiceRect.pivot = new Vector2(0f, 1f);
            choiceRect.anchoredPosition = Vector2.zero;
            choiceRect.sizeDelta = new Vector2(0f, 30f);
            choiceRect.localScale = Vector3.one;
            UIImage choiceImage = choice.AddComponent<UIImage>();
            choiceImage.color = Color.clear;
            Button choiceButton = choice.AddComponent<Button>();
            choiceButton.targetGraphic = choiceImage;
            choiceButton.transition = Selectable.Transition.None;
            AddRuntimeComponent(
                choice,
                "GameLogic.JxqyChoiceButtonEventRelay, GameLogic");
            CreateTextStretch(
                "m_text_Name",
                choice.transform,
                18,
                TextAnchor.MiddleCenter,
                new Color(0f, 1f, 0f, 0.8f),
                Vector2.zero,
                Vector2.zero);
            choice.SetActive(false);
            SaveResponsive(root);
        }

        private static void CreateLittleMapButton(
            Transform parent,
            string name,
            string fileName,
            float left,
            float top,
            bool hold)
        {
            Button button = CreateAsfButton(
                parent,
                $"m_btn_{name}",
                $"littlemap/{fileName}",
                left,
                top,
                24f,
                24f);
            if (hold)
            {
                AddRuntimeComponent(
                    button.gameObject,
                    "GameLogic.JxqyPointerHoldRelay, GameLogic");
            }
        }

        private static void AddCooldownNodesToExistingSlotPrefabs()
        {
            string[] names =
            {
                "JxqyHudUI",
                "JxqyStatusUI",
                "JxqyInventoryUI",
                "JxqyEquipmentUI",
                "JxqyTrainingUI",
                "JxqySkillsUI",
                "JxqyTradeUI",
                "JxqyTradeGoodsUI",
                "JxqySaveLoadUI",
            };
            foreach (string name in names)
            {
                string path = $"{OutputDirectory}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    RectTransform[] candidates =
                        root.GetComponentsInChildren<RectTransform>(true);
                    foreach (RectTransform candidate in candidates)
                    {
                        if (!candidate.name.StartsWith(
                                "m_item_",
                                StringComparison.Ordinal) ||
                            candidate.GetComponent<Button>() == null ||
                            candidate.Find("m_raw_Icon") == null &&
                            candidate.Find("m_text_Detail") == null ||
                            candidate.Find("m_img_Cooldown") != null)
                        {
                            continue;
                        }
                        UIImage cooldown = CreateImageStretch(
                            "m_img_Cooldown",
                            candidate,
                            new Color(0f, 0f, 0f, 0.62f));
                        CreateTextStretch(
                            "m_text_Cooldown",
                            cooldown.transform,
                            12,
                            TextAnchor.MiddleCenter,
                            Color.white,
                            Vector2.zero,
                            Vector2.zero);
                        cooldown.gameObject.SetActive(false);
                        changed = true;
                    }
                    if (changed)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void EnsureInventoryScrollTrack()
        {
            string path =
                $"{OutputDirectory}/JxqyInventoryUI.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.transform.Find("m_img_ScrollTrack") != null)
                    return;
                RectTransform thumb = root.transform
                    .Find("m_raw_ScrollThumb") as RectTransform;
                if (thumb == null)
                    throw new InvalidOperationException(
                        "JxqyInventoryUI has no serialized scroll thumb.");
                UIImage track = CreateImage(
                    "m_img_ScrollTrack",
                    root.transform,
                    694f,
                    108f,
                    28f,
                    190f,
                    Color.clear);
                track.raycastTarget = true;
                track.transform.SetSiblingIndex(thumb.GetSiblingIndex());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureTradeScrollTracks()
        {
            EnsureScrollTrack(
                "JxqyTradeUI",
                "m_img_ShopScrollTrack",
                "m_raw_ShopScrollThumb");
            EnsureScrollTrack(
                "JxqyTradeGoodsUI",
                "m_img_InventoryScrollTrack",
                "m_raw_InventoryScrollThumb");
        }

        private static void EnsureScrollTrack(
            string prefabName,
            string trackName,
            string thumbName)
        {
            string path = $"{OutputDirectory}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RectTransform thumb = null;
                RectTransform[] descendants =
                    root.GetComponentsInChildren<RectTransform>(true);
                foreach (RectTransform descendant in descendants)
                {
                    if (descendant.name == trackName)
                        return;
                    if (descendant.name == thumbName)
                        thumb = descendant;
                }
                if (thumb == null)
                    throw new InvalidOperationException(
                        $"{prefabName} has no serialized scroll thumb.");
                GameObject trackObject = CreateUiObject(
                    trackName,
                    thumb.parent);
                RectTransform trackRect =
                    trackObject.GetComponent<RectTransform>();
                trackRect.anchorMin = thumb.anchorMin;
                trackRect.anchorMax = thumb.anchorMax;
                trackRect.pivot = thumb.pivot;
                trackRect.sizeDelta = new Vector2(28f, 190f);
                Vector2 thumbSize = thumb.sizeDelta;
                trackRect.anchoredPosition = thumb.anchoredPosition +
                    new Vector2(
                        (28f - thumbSize.x) * 0.5f,
                        -(190f - thumbSize.y) * 0.5f);
                UIImage track = trackObject.AddComponent<UIImage>();
                track.color = Color.clear;
                track.raycastTarget = true;
                track.transform.SetSiblingIndex(thumb.GetSiblingIndex());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildCombatFloatTextView()
        {
            GameObject root = CreateUiObject(
                "JxqyCombatFloatTextView",
                null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(120f, 58f);
            UIText popup = CreateText(
                "m_text_Value",
                root.transform,
                0f,
                13f,
                120f,
                32f,
                16,
                TextAnchor.MiddleCenter,
                Color.white);
            popup.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            popup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            popup.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            popup.rectTransform.anchoredPosition = Vector2.zero;
            popup.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddOutline(popup);
            SaveCombatFloatTextView(root);
        }

        private static void BuildDialogue()
        {
            GameObject root = CreateCanvas("JxqyDialogueUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "dialog/panel.asf",
                DialoguePanelLeft,
                DialoguePanelTop);
            CreateTransparentRawImage(
                "m_raw_Portrait",
                root.transform,
                DialoguePanelLeft + 5f,
                DialoguePanelTop - 143f,
                200,
                160,
                centered: true);
            Button continueButton = CreateTransparentButton(
                "m_btn_Continue",
                root.transform,
                DialoguePanelLeft,
                DialoguePanelTop,
                438,
                123);
            continueButton.targetGraphic.raycastTarget = true;
            UIText message = CreateText(
                "m_text_Message",
                root.transform,
                DialoguePanelLeft + 65f,
                DialoguePanelTop + 30f,
                310,
                70,
                19,
                TextAnchor.UpperLeft,
                new Color32(0, 0, 0, 255));
            message.font = _dialogueFont;
            CreateSlot(
                root.transform,
                "m_item_Choice0",
                DialoguePanelLeft + 65f,
                DialoguePanelTop + 52f,
                310,
                20,
                13,
                true);
            CreateSlot(
                root.transform,
                "m_item_Choice1",
                DialoguePanelLeft + 65f,
                DialoguePanelTop + 74f,
                310,
                20,
                13,
                true);
            Save(root);
        }

        private static void BuildStatus()
        {
            GameObject root = CreateCanvas("JxqyStatusUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "common/panel5.asf",
                LeftPanel,
                0);
            string[] names =
            {
                "m_text_Level",
                "m_text_Experience",
                "m_text_LevelUp",
                "m_text_Life",
                "m_text_Thew",
                "m_text_Mana",
                "m_text_Attack",
                "m_text_Defend",
                "m_text_Evade",
            };
            float[] top =
            {
                219, 234, 249, 264, 279, 294, 309, 324, 339,
            };
            for (int index = 0; index < names.Length; index++)
            {
                CreateText(
                    names[index],
                    root.transform,
                    LeftPanel + 144,
                    top[index],
                    100,
                    14,
                    12,
                    TextAnchor.MiddleLeft,
                    new Color32(0, 0, 0, 210));
            }
            CreatePlainButton(
                "m_btn_Close",
                "返回",
                root.transform,
                LeftPanel + 389f,
                392,
                70,
                30);
            Save(root);
        }

        private static void BuildMemo()
        {
            GameObject root = CreateCanvas("JxqyMemoUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "common/panel4.asf",
                RightPanel,
                0f);
            CreateText(
                "m_text_Memo",
                root.transform,
                RightPanel + 90f,
                155f,
                150f,
                180f,
                12,
                TextAnchor.UpperLeft,
                new Color32(40, 25, 15, 204));
            UIImage track = CreateImage(
                "m_img_ScrollTrack",
                root.transform,
                RightPanel + 295f,
                108f,
                28f,
                190f,
                Color.clear);
            track.raycastTarget = true;
            RawImage thumb = CreateAsfImage(
                root.transform,
                "m_raw_ScrollThumb",
                "option/slidebtn.asf",
                RightPanel + 295f,
                108f);
            thumb.raycastTarget = true;
            Save(root);
        }

        private static void BuildInventory()
        {
            GameObject root = CreateCanvas("JxqyInventoryUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "common/panel3.asf",
                RightPanel,
                0);
            CreateNineGrid(
                root.transform,
                "m_item_Slot",
                RightPanel,
                new[] { 71f, 137f, 201f },
                new[] { 91f, 170f, 250f });
            CreateText(
                "m_text_Money",
                root.transform,
                RightPanel + 137,
                363,
                100,
                16,
                13,
                TextAnchor.MiddleLeft,
                Color.white);
            UIImage track = CreateImage(
                "m_img_ScrollTrack",
                root.transform,
                RightPanel + 294f,
                108f,
                28f,
                190f,
                Color.clear);
            track.raycastTarget = true;
            RawImage thumb = CreateAsfImage(
                root.transform,
                "m_raw_ScrollThumb",
                "option/slidebtn.asf",
                RightPanel + 294,
                108);
            thumb.raycastTarget = true;
            Save(root);
        }

        private static void BuildItemDetail()
        {
            BuildLegacyDetail("JxqyItemDetailUI");
        }

        private static void BuildEquipment()
        {
            GameObject root = CreateCanvas("JxqyEquipmentUI");
            CreateAsfImage(
                root.transform,
                "m_raw_EquipmentPanel",
                "common/panel7.asf",
                LeftPanel,
                0);
            float[,] equipped =
            {
                { 47, 66 },
                { 193, 66 },
                { 47, 168 },
                { 121, 168 },
                { 193, 168 },
                { 47, 267 },
                { 193, 267 },
            };
            for (int index = 0; index < equipped.GetLength(0); index++)
            {
                CreateSlot(
                    root.transform,
                    $"m_item_Equipped{index + 1}",
                    LeftPanel + equipped[index, 0],
                    equipped[index, 1],
                    60,
                    75,
                    11);
            }
            Save(root);
        }

        private static void BuildSkills()
        {
            GameObject root = CreateCanvas("JxqySkillsUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "common/panel2.asf",
                RightPanel,
                0);
            CreateNineGrid(
                root.transform,
                "m_item_Slot",
                RightPanel,
                new[] { 71f, 137f, 201f },
                new[] { 91f, 170f, 250f });
            UIImage track = CreateImage(
                "m_img_ScrollTrack",
                root.transform,
                RightPanel + 294f,
                108f,
                28f,
                190f,
                Color.clear);
            track.raycastTarget = true;
            RawImage thumb = CreateAsfImage(
                root.transform,
                "m_raw_ScrollThumb",
                "option/slidebtn.asf",
                RightPanel + 294,
                108);
            thumb.raycastTarget = true;
            CreatePlainButton(
                "m_btn_Select",
                "选用",
                root.transform,
                RightPanel + 40f,
                402,
                55,
                28);
            CreatePlainButton(
                "m_btn_Close",
                "返回",
                root.transform,
                RightPanel + 100f,
                402,
                55,
                28);
            Save(root);
        }

        private static void BuildTraining()
        {
            GameObject root = CreateCanvas("JxqyTrainingUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "common/panel6.asf",
                LeftPanel,
                0);
            CreateSlot(
                root.transform,
                "m_item_Cultivation",
                LeftPanel + 115f,
                75f,
                60f,
                75f,
                11);
            CreateText(
                "m_text_Level",
                root.transform,
                LeftPanel + 126f,
                224f,
                80f,
                12f,
                11,
                TextAnchor.MiddleLeft,
                new Color32(0, 0, 0, 204));
            CreateText(
                "m_text_Experience",
                root.transform,
                LeftPanel + 126f,
                243f,
                80f,
                12f,
                11,
                TextAnchor.MiddleLeft,
                new Color32(0, 0, 0, 204));
            CreateText(
                "m_text_MagicName",
                root.transform,
                LeftPanel + 105f,
                256f,
                200f,
                20f,
                16,
                TextAnchor.MiddleLeft,
                new Color32(88, 32, 32, 229));
            CreateText(
                "m_text_Introduction",
                root.transform,
                LeftPanel + 75f,
                275f,
                145f,
                120f,
                13,
                TextAnchor.UpperLeft,
                new Color32(47, 32, 88, 229));
            Save(root);
        }

        private static void BuildMagicDetail()
        {
            BuildLegacyDetail("JxqyMagicDetailUI");
        }

        private static void BuildLegacyDetail(string windowName)
        {
            GameObject root = CreateCanvas(windowName);
            CreateTransparentButton(
                "m_btn_Mask",
                root.transform,
                0f,
                0f,
                JxqyLogicalViewport.OriginalWidth,
                JxqyLogicalViewport.OriginalHeight);
            CreateLegacyTooltip(root.transform);
            CreatePlainButton(
                "m_btn_Close",
                "关闭",
                root.transform,
                474f,
                386f,
                55f,
                28f);
            Save(root);
        }

        private static void BuildTrade()
        {
            GameObject root = CreateCanvas("JxqyTradeUI");
            CreateAsfImage(
                root.transform,
                "m_raw_ShopPanel",
                "common/panel8.asf",
                LeftPanel,
                0);
            CreateNineGrid(
                root.transform,
                "m_item_Shop",
                LeftPanel,
                new[] { 55f, 120f, 184f },
                new[] { 91f, 170f, 250f });
            UIImage track = CreateImage(
                "m_img_ShopScrollTrack",
                root.transform,
                LeftPanel + 271f,
                108f,
                28f,
                190f,
                Color.clear);
            track.raycastTarget = true;
            CreateAsfImage(
                root.transform,
                "m_raw_ShopScrollThumb",
                "option/slidebtn.asf",
                LeftPanel + 271,
                108);
            CreatePlainButton(
                "m_btn_Buy",
                "买入",
                root.transform,
                LeftPanel + 110f,
                392,
                55,
                28);
            CreateAsfButton(
                root.transform,
                "m_btn_Close",
                "buysell/CloseBtn.asf",
                LeftPanel + 117,
                354,
                63,
                64);
            Save(root);
        }

        private static void BuildTradeGoods()
        {
            GameObject root = CreateCanvas("JxqyTradeGoodsUI");
            CreateAsfImage(
                root.transform,
                "m_raw_InventoryPanel",
                "common/panel3.asf",
                RightPanel,
                0);
            CreateNineGrid(
                root.transform,
                "m_item_Inventory",
                RightPanel,
                new[] { 71f, 137f, 201f },
                new[] { 91f, 170f, 250f });
            CreateText(
                "m_text_Money",
                root.transform,
                RightPanel + 137,
                363,
                100,
                12,
                13,
                TextAnchor.MiddleLeft,
                Color.white);
            UIImage track = CreateImage(
                "m_img_InventoryScrollTrack",
                root.transform,
                RightPanel + 294f,
                108f,
                28f,
                190f,
                Color.clear);
            track.raycastTarget = true;
            CreateAsfImage(
                root.transform,
                "m_raw_InventoryScrollThumb",
                "option/slidebtn.asf",
                RightPanel + 294,
                108);
            CreatePlainButton(
                "m_btn_Sell",
                "卖出",
                root.transform,
                RightPanel + 155f,
                392,
                55,
                28);
            Save(root);
        }

        private static void BuildMenu()
        {
            GameObject root = CreateCanvas("JxqyMenuUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "common/panel.asf",
                (LogicalWidth - 184f) / 2f,
                26);
            CreateAsfButton(
                root.transform,
                "m_btn_SaveLoad",
                "system/saveload.asf",
                (LogicalWidth - 184f) / 2f + 58f,
                112,
                69,
                64);
            CreateAsfButton(
                root.transform,
                "m_btn_Option",
                "system/option.asf",
                (LogicalWidth - 184f) / 2f + 58f,
                176,
                69,
                54);
            CreateAsfButton(
                root.transform,
                "m_btn_Quit",
                "system/quit.asf",
                (LogicalWidth - 184f) / 2f + 58f,
                239,
                69,
                54);
            CreateAsfButton(
                root.transform,
                "m_btn_Return",
                "system/return.asf",
                (LogicalWidth - 184f) / 2f + 58f,
                302,
                69,
                54);
            CreateText(
                "m_text_Message",
                root.transform,
                SaveLoadPanelLeft + 160f,
                382,
                320,
                36,
                15,
                TextAnchor.MiddleCenter,
                new Color32(255, 215, 0, 230));
            Save(root);
        }

        private static void BuildSaveLoad()
        {
            GameObject root = CreateCanvas("JxqySaveLoadUI");
            CreateAsfImage(
                root.transform,
                "m_raw_Panel",
                "saveload/panel.asf",
                SaveLoadPanelLeft,
                SaveLoadPanelTop);
            for (int index = 0; index < 7; index++)
            {
                CreateSlot(
                    root.transform,
                    $"m_item_Slot{index + 1}",
                    SaveLoadPanelLeft + 135f,
                    SaveLoadPanelTop + 118f + index * 25f,
                    100,
                    24,
                    13,
                    true);
            }
            CreateTransparentRawImage(
                "m_raw_Snapshot",
                root.transform,
                SaveLoadPanelLeft + 256f,
                SaveLoadPanelTop + 94f,
                267,
                200);
            CreateText(
                "m_text_Description",
                root.transform,
                SaveLoadPanelLeft + 270f,
                SaveLoadPanelTop + 120f,
                235,
                120,
                16,
                TextAnchor.MiddleCenter,
                new Color32(182, 219, 189, 220));
            CreateText(
                "m_text_SavedAt",
                root.transform,
                SaveLoadPanelLeft + 254f,
                SaveLoadPanelTop + 310f,
                350,
                30,
                13,
                TextAnchor.MiddleLeft,
                new Color32(182, 219, 189, 220));
            CreateText(
                "m_text_Message",
                root.transform,
                SaveLoadPanelLeft,
                SaveLoadPanelTop + 440f,
                640,
                40,
                15,
                TextAnchor.MiddleCenter,
                new Color32(255, 215, 0, 220));
            CreateAsfButton(
                root.transform,
                "m_btn_Load",
                "saveload/btnLoad.asf",
                SaveLoadPanelLeft + 248f,
                SaveLoadPanelTop + 355f,
                64,
                72);
            CreateAsfButton(
                root.transform,
                "m_btn_Save",
                "saveload/btnSave.asf",
                SaveLoadPanelLeft + 366f,
                SaveLoadPanelTop + 355f,
                64,
                72);
            CreateAsfButton(
                root.transform,
                "m_btn_Exit",
                "saveload/btnExit.asf",
                SaveLoadPanelLeft + 464f,
                SaveLoadPanelTop + 355f,
                64,
                72);
            Save(root);
        }

        private static void CreateNineGrid(
            Transform parent,
            string prefix,
            float panelLeft,
            float[] left,
            float[] top)
        {
            int index = 1;
            for (int row = 0; row < top.Length; row++)
            {
                for (int column = 0; column < left.Length; column++)
                {
                    CreateSlot(
                        parent,
                        $"{prefix}{index}",
                        panelLeft + left[column],
                        top[row],
                        60,
                        75,
                        11);
                    index++;
                }
            }
        }

        private static GameObject CreateCanvas(string name)
        {
            GameObject root = CreateUiObject(name, null);
            Canvas canvas = root.AddComponent<Canvas>();
            // TEngine windows are child canvases under UIRoot/UICanvas. The
            // framework root owns ScreenSpaceCamera and CanvasScaler; putting
            // another screen-space canvas/scaler on every window makes Unity
            // drive the prefab root to a zero-sized, zero-scale RectTransform.
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<GraphicRaycaster>();
            SetLogicalRoot(root.GetComponent<RectTransform>());
            return root;
        }

        private static RawImage CreateAsfImage(
            Transform parent,
            string name,
            string relativePath,
            float left,
            float top,
            float width = -1,
            float height = -1)
        {
            LoadAsfFrame(
                relativePath,
                0,
                out Texture2D atlas,
                out JxqyAnimationMetadata metadata,
                out JxqyAnimationFrameMetadata frame);
            float scaleX = width > 0
                ? width / metadata.GlobalWidth
                : 1f;
            float scaleY = height > 0
                ? height / metadata.GlobalHeight
                : 1f;
            float drawWidth = frame.AtlasWidth * scaleX;
            float drawHeight = frame.AtlasHeight * scaleY;
            float drawLeft = left + frame.TrimLeft * scaleX;
            float drawTop = top +
                (metadata.GlobalHeight -
                 frame.TrimBottom -
                 frame.AtlasHeight) * scaleY;
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                drawLeft,
                drawTop,
                drawWidth,
                drawHeight);
            RawImage image = value.AddComponent<RawImage>();
            image.texture = atlas;
            image.uvRect = FrameUv(atlas, frame);
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateAsfButton(
            Transform parent,
            string name,
            string relativePath,
            float left,
            float top,
            float width,
            float height)
        {
            RawImage image = CreateAsfImage(
                parent,
                name,
                relativePath,
                left,
                top,
                width,
                height);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static void CreateMeter(
            Transform parent,
            string name,
            string relativePath,
            float left,
            float top)
        {
            LoadAsfFrame(
                relativePath,
                0,
                out Texture2D atlas,
                out JxqyAnimationMetadata metadata,
                out JxqyAnimationFrameMetadata frame);
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                metadata.GlobalWidth,
                metadata.GlobalHeight);
            Type filledType = Type.GetType(
                "GameLogic.JxqyFilledRawImage, GameLogic");
            if (filledType == null ||
                !typeof(RawImage).IsAssignableFrom(filledType))
            {
                throw new InvalidOperationException(
                    "JxqyFilledRawImage runtime component is unavailable.");
            }
            RawImage image = (RawImage)value.AddComponent(filledType);
            image.texture = atlas;
            image.uvRect = FrameUv(atlas, frame);
            image.raycastTarget = false;
        }

        private static void CreateLegacyNotice(Transform parent)
        {
            const float panelLeft = 272f;
            const float panelTop = 459f;
            CreateAsfImage(
                parent,
                "m_raw_NoticePanel",
                "message/msgbox.asf",
                panelLeft,
                panelTop);
            CreateText(
                "m_text_Notice",
                parent,
                panelLeft + 46f,
                panelTop + 32f,
                148f,
                50f,
                12,
                TextAnchor.UpperLeft,
                new Color32(155, 34, 22, 204));
        }

        private static void CreateLegacyTooltip(Transform parent)
        {
            GameObject group = CreateUiObject("m_group_Tooltip", parent);
            Stretch(group.GetComponent<RectTransform>());
            const float panelLeft = 232f;
            const float panelTop = 27f;
            RawImage panel = CreateAsfImage(
                group.transform,
                "m_raw_TooltipPanel",
                "common/tipbox.asf",
                panelLeft,
                panelTop);
            // Block clicks inside the detail panel from falling through to the
            // full-screen close mask behind it.
            panel.raycastTarget = true;
            CreateTransparentRawImage(
                "m_raw_TooltipIcon",
                group.transform,
                panelLeft + 132f,
                panelTop + 47f,
                60f,
                75f,
                centered: true);
            CreateText(
                "m_text_TooltipName",
                group.transform,
                panelLeft + 67f,
                panelTop + 191f,
                100f,
                20f,
                13,
                TextAnchor.MiddleLeft,
                new Color32(102, 73, 212, 204));
            CreateText(
                "m_text_TooltipMeta",
                group.transform,
                panelLeft + 160f,
                panelTop + 191f,
                110f,
                20f,
                13,
                TextAnchor.MiddleLeft,
                new Color32(91, 31, 27, 204));
            CreateText(
                "m_text_TooltipEffect",
                group.transform,
                panelLeft + 67f,
                panelTop + 215f,
                196f,
                28f,
                13,
                TextAnchor.UpperLeft,
                new Color32(0, 0, 255, 204));
            CreateText(
                "m_text_TooltipIntro",
                group.transform,
                panelLeft + 67f,
                panelTop + 245f,
                196f,
                110f,
                13,
                TextAnchor.UpperLeft,
                new Color32(52, 21, 14, 204));
            group.SetActive(false);
        }

        private static void CreateSlot(
            Transform parent,
            string name,
            float left,
            float top,
            float width,
            float height,
            int fontSize,
            bool textOnly = false)
        {
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                width,
                height);
            UIImage image = value.AddComponent<UIImage>();
            image.color = Color.clear;
            Button button = value.AddComponent<Button>();
            button.targetGraphic = image;
            if (!textOnly)
            {
                GameObject icon = CreateUiObject(
                    "m_raw_Icon",
                    value.transform);
                RectTransform iconRect =
                    icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(width, height);
                iconRect.localScale = Vector3.one;
                RawImage iconImage = icon.AddComponent<RawImage>();
                iconImage.color = Color.clear;
                iconImage.raycastTarget = false;
            }
            UIText nameText = CreateTextStretch(
                "m_text_Name",
                value.transform,
                fontSize,
                TextAnchor.MiddleCenter,
                textOnly
                    ? new Color32(0, 0, 204, 255)
                    : new Color32(70, 37, 22, 235),
                new Vector2(2, 13),
                new Vector2(-2, -2));
            nameText.gameObject.SetActive(textOnly);
            UIText detailText = CreateText(
                "m_text_Detail",
                value.transform,
                2,
                2,
                width - 4,
                14,
                Math.Max(9, fontSize - 1),
                TextAnchor.UpperLeft,
                Color.white);
            AddOutline(detailText);
            detailText.gameObject.SetActive(!textOnly);

            UIImage cooldown = CreateImageStretch(
                "m_img_Cooldown",
                value.transform,
                new Color(0f, 0f, 0f, 0.62f));
            CreateTextStretch(
                "m_text_Cooldown",
                cooldown.transform,
                12,
                TextAnchor.MiddleCenter,
                Color.white,
                Vector2.zero,
                Vector2.zero);
            cooldown.gameObject.SetActive(false);
        }

        private static Button CreateTransparentButton(
            string name,
            Transform parent,
            float left,
            float top,
            float width,
            float height)
        {
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                width,
                height);
            UIImage image = value.AddComponent<UIImage>();
            image.color = Color.clear;
            Button button = value.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Button CreatePlainButton(
            string name,
            string caption,
            Transform parent,
            float left,
            float top,
            float width,
            float height)
        {
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                width,
                height);
            UIImage image = value.AddComponent<UIImage>();
            image.color = Color.clear;
            Button button = value.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static UIImage CreateImage(
            string name,
            Transform parent,
            float left,
            float top,
            float width,
            float height,
            Color color)
        {
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                width,
                height);
            UIImage image = value.AddComponent<UIImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage CreateTransparentRawImage(
            string name,
            Transform parent,
            float left,
            float top,
            float width,
            float height,
            bool centered = false)
        {
            GameObject value = CreateUiObject(name, parent);
            RectTransform rect = value.GetComponent<RectTransform>();
            if (centered)
                SetTopLeftCentered(rect, left, top, width, height);
            else
                SetTopLeft(rect, left, top, width, height);
            RawImage image = value.AddComponent<RawImage>();
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }

        private static UIImage CreateImageStretch(
            string name,
            Transform parent,
            Color color)
        {
            GameObject value = CreateUiObject(name, parent);
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            UIImage image = value.AddComponent<UIImage>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static UIText CreateText(
            string name,
            Transform parent,
            float left,
            float top,
            float width,
            float height,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject value = CreateUiObject(name, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                width,
                height);
            return ConfigureText(
                value.AddComponent<UIText>(),
                fontSize,
                alignment,
                color);
        }

        private static UIText CreateTextStretch(
            string name,
            Transform parent,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject value = CreateUiObject(name, parent);
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            return ConfigureText(
                value.AddComponent<UIText>(),
                fontSize,
                alignment,
                color);
        }

        private static UIText ConfigureText(
            UIText text,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            text.font = _originalFont != null
                ? _originalFont
                : AssetDatabase.LoadAssetAtPath<UIFont>(
                    NativeFontAssetPath);
            if (text.font == null)
                throw new InvalidDataException(
                    $"Jxqy native UI font is missing: " +
                    $"{NativeFontAssetPath}");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Outline AddOutline(UIText text)
        {
            Outline outline = text.GetComponent<Outline>() ??
                              text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
            return outline;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.layer = LayerMask.NameToLayer("UI");
            value.transform.SetParent(parent, false);
            if (parent == null)
                SetLogicalRoot(value.GetComponent<RectTransform>());
            return value;
        }

        private static Component AddRuntimeComponent(
            GameObject target,
            string assemblyQualifiedTypeName)
        {
            Type type = Type.GetType(assemblyQualifiedTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"Runtime UI component type is unavailable: " +
                    $"{assemblyQualifiedTypeName}");
            }
            return target.GetComponent(type) ?? target.AddComponent(type);
        }

        private static void LoadAsfFrame(
            string relativePath,
            int frameIndex,
            out Texture2D atlas,
            out JxqyAnimationMetadata metadata,
            out JxqyAnimationFrameMetadata frame)
        {
            string directory = $"{UiAnimationRoot}/{relativePath}";
            TextAsset metadataAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"{directory}/animation.json");
            if (metadataAsset == null)
                throw new FileNotFoundException(
                    "UI animation metadata is missing.",
                    $"{directory}/animation.json");
            metadata = JsonUtility.FromJson<JxqyAnimationMetadata>(
                metadataAsset.text);
            if (metadata?.Frames == null ||
                frameIndex < 0 ||
                frameIndex >= metadata.Frames.Count)
                throw new InvalidDataException(
                    $"UI animation has no frame {frameIndex}: " +
                    relativePath);
            frame = metadata.Frames[frameIndex];
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{directory}/animation.atlas." +
                $"{frame.AtlasPage:D3}.png");
            if (atlas == null)
                throw new FileNotFoundException(
                    "UI animation atlas is missing.",
                    directory);
        }

        private static Rect FrameUv(
            Texture2D atlas,
            JxqyAnimationFrameMetadata frame)
        {
            return new Rect(
                (float)frame.AtlasX / atlas.width,
                (float)frame.AtlasY / atlas.height,
                (float)frame.AtlasWidth / atlas.width,
                (float)frame.AtlasHeight / atlas.height);
        }

        private static void SetTopLeft(
            RectTransform rect,
            float left,
            float top,
            float width,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void SetTopLeftCentered(
            RectTransform rect,
            float left,
            float top,
            float width,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                left + width * 0.5f,
                -top - height * 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetLogicalRoot(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(LogicalWidth, LogicalHeight);
            rect.localScale = Vector3.one;
        }

        private static void ReplacePrefabFonts()
        {
            foreach (string windowName in WindowNames)
            {
                string path =
                    $"{OutputDirectory}/{windowName}.prefab";
                if (!PrefabExists(windowName))
                    continue;
                GameObject root =
                    PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (UIText text in
                             root.GetComponentsInChildren<UIText>(true))
                    {
                        UIFont expectedFont =
                            windowName == "JxqyDialogueUI" &&
                            text.name == "m_text_Message"
                                ? _dialogueFont
                                : _originalFont;
                        if (text.font == expectedFont)
                            continue;
                        text.font = expectedFont;
                        EditorUtility.SetDirty(text);
                        changed = true;
                    }
                    if (changed)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static UIFont BuildDialogueFont()
        {
            UIFont font = AssetDatabase.LoadAssetAtPath<UIFont>(
                DialogueFontAssetPath);
            if (font == null)
            {
                if (!AssetDatabase.CopyAsset(
                        NativeFontAssetPath,
                        DialogueFontAssetPath))
                {
                    throw new IOException(
                        $"Cannot create the original dialogue font: " +
                        $"{DialogueFontAssetPath}");
                }
                AssetDatabase.ImportAsset(
                    DialogueFontAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                font = AssetDatabase.LoadAssetAtPath<UIFont>(
                    DialogueFontAssetPath);
            }
            if (font == null)
                throw new InvalidDataException(
                    $"Jxqy dialogue font is missing: " +
                    $"{DialogueFontAssetPath}");

            font.characterInfo = _originalFont.characterInfo;
            font.material = _originalFont.material;
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void Save(GameObject root)
        {
            SetLogicalRoot(root.GetComponent<RectTransform>());
            string path = $"{OutputDirectory}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(
                root,
                path,
                out bool success);
            UnityEngine.Object.DestroyImmediate(root);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RectTransform rect =
                prefab == null ? null : prefab.GetComponent<RectTransform>();
            if (!success || rect == null ||
                rect.localScale != Vector3.one ||
                rect.anchorMin != new Vector2(0.5f, 0.5f) ||
                rect.anchorMax != new Vector2(0.5f, 0.5f) ||
                rect.sizeDelta != new Vector2(
                    LogicalWidth,
                    LogicalHeight))
            {
                throw new InvalidDataException(
                    $"Generated UI prefab root is not renderable: {path}");
            }
        }

        private static void SaveResponsive(GameObject root)
        {
            Stretch(root.GetComponent<RectTransform>());
            string path = $"{OutputDirectory}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(
                root,
                path,
                out bool success);
            UnityEngine.Object.DestroyImmediate(root);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RectTransform rect =
                prefab == null ? null : prefab.GetComponent<RectTransform>();
            if (!success || rect == null ||
                rect.localScale != Vector3.one ||
                rect.anchorMin != Vector2.zero ||
                rect.anchorMax != Vector2.one ||
                rect.sizeDelta != Vector2.zero)
            {
                throw new InvalidDataException(
                    $"Generated responsive UI prefab root is invalid: {path}");
            }
        }

        private static void SaveCombatFloatTextView(GameObject root)
        {
            string path = $"{OutputDirectory}/{root.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(
                root,
                path,
                out bool success);
            UnityEngine.Object.DestroyImmediate(root);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            RectTransform rect =
                prefab == null ? null : prefab.GetComponent<RectTransform>();
            if (!success || rect == null ||
                rect.sizeDelta != new Vector2(120f, 58f))
            {
                throw new InvalidDataException(
                    $"Generated combat float-text prefab is invalid: {path}");
            }
        }
    }
}
