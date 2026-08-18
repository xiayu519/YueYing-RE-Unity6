using System;
using System.Collections.Generic;
using System.IO;
using Jxqy.Domain.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UIFont = UnityEngine.Font;
using UIImage = UnityEngine.UI.Image;
using UIText = UnityEngine.UI.Text;

namespace Jxqy.Editor.UI
{
    [InitializeOnLoad]
    public static class JxqyHudMobileControlsInstaller
    {
        private const string PrefabPath =
            "Assets/AssetRaw/Jxqy/UI/Prefabs/JxqyHudUI.prefab";
        private const string FontPath =
            "Assets/AssetRaw/Jxqy/UI/Fonts/FZZhengYuan.ttf";
        private const string JoystickBackgroundPath =
            "Assets/AssetRaw/UIRaw/Atlas/Battle/" +
            "Play_Joystick_bg.png";
        private const string JoystickHandlePath =
            "Assets/AssetRaw/UIRaw/Atlas/Battle/" +
            "Play_Joystick_handle.png";
        private static readonly string RequestPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "Temp",
            "JxqyValidation",
            "install-mobile-hud.request");
        private static readonly string ResultPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
            "Temp",
            "JxqyValidation",
            "install-mobile-hud.result");

        static JxqyHudMobileControlsInstaller()
        {
            EditorApplication.update += PollRequest;
        }

        [MenuItem("TEngine/Jxqy/Install HUD Mobile Controls")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool changed;
            try
            {
                List<string> originalHierarchy = CaptureHierarchy(root);
                Transform existing = root.transform.Find(
                    "m_go_MobileControls");
                if (existing == null)
                {
                    BuildMobileControls(root.transform);
                    changed = true;
                }
                else
                {
                    changed = AddMissingMobileButtons(existing);
                }
                RequireOriginalHierarchyPreserved(root, originalHierarchy);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (changed)
            {
                // Avoid reserializing an already-complete user-owned prefab.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log(
                changed
                    ? "Jxqy HUD mobile controls installed without " +
                      "rebuilding the existing prefab hierarchy."
                    : "Jxqy HUD mobile controls are already complete.");
        }

        private static void PollRequest()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                !File.Exists(RequestPath))
            {
                return;
            }
            File.Delete(RequestPath);
            try
            {
                Install();
                Directory.CreateDirectory(
                    Path.GetDirectoryName(ResultPath) ?? string.Empty);
                File.WriteAllText(ResultPath, $"Passed|{DateTime.UtcNow:O}");
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, $"Failed|{exception}");
                throw;
            }
        }

        private static void BuildMobileControls(Transform parent)
        {
            GameObject controls = CreateUiObject(
                "m_go_MobileControls",
                parent);
            Stretch(controls.GetComponent<RectTransform>());
            AddRuntimeComponent(
                controls,
                "Jxqy.UnityAdapters.JxqyMobileControlsVisibility, " +
                "Jxqy.Runtime");
            AddRuntimeComponent(
                controls,
                "Jxqy.UnityAdapters.JxqyMobileWorldTouchInput, " +
                "Jxqy.Runtime");

            UIImage background = CreateSpriteImage(
                "m_img_MobileJoystickBackground",
                controls.transform,
                JoystickBackgroundPath,
                20f,
                390f,
                170f,
                170f,
                new Color(1f, 1f, 1f, 0.72f));
            background.raycastTarget = true;
            UIImage handle = CreateSpriteImage(
                "m_img_MobileJoystickHandle",
                background.transform,
                JoystickHandlePath,
                0f,
                0f,
                62f,
                68f,
                new Color(1f, 1f, 1f, 0.9f));
            SetCentered(handle.rectTransform, 62f, 68f);
            Component joystick = AddRuntimeComponent(
                background.gameObject,
                "Jxqy.UnityAdapters.JxqyVirtualJoystickInput, Jxqy.Runtime");
            SetSerializedReference(
                joystick,
                "_movementArea",
                background.rectTransform);
            SetSerializedReference(joystick, "_handle", handle.rectTransform);
            SetSerializedFloat(joystick, "_radius", 72f);

            CreateModifierButton(
                controls.transform,
                "m_btn_MobileRun",
                "m_text_MobileRun",
                "快跑\nShift",
                714f,
                278f,
                72f,
                64f,
                1 << 10);
            CreateActionButton(
                controls.transform,
                "m_btn_MobileAttack",
                "m_text_MobileAttack",
                "攻击",
                700f,
                350f,
                88f,
                88f,
                JxqyInputIntentKind.MobileDirectionalAttack);
            CreateModifierButton(
                controls.transform,
                "m_btn_MobileJump",
                "m_text_MobileJump",
                "跳跃\nAlt",
                624f,
                380f,
                68f,
                68f,
                1 << 11);

            string[] skillKeys = { "A", "S", "D", "F", "G" };
            for (int index = 0; index < skillKeys.Length; index++)
            {
                CreateActionButton(
                    controls.transform,
                    $"m_btn_MobileSkill{skillKeys[index]}",
                    $"m_text_MobileSkill{skillKeys[index]}",
                    skillKeys[index],
                    532f + index * 52f,
                    462f,
                    48f,
                    48f,
                    JxqyInputIntentKind.MobileDirectionalSkill,
                    index);
            }

            string[] itemKeys = { "Z", "X", "C" };
            for (int index = 0; index < itemKeys.Length; index++)
            {
                CreateActionButton(
                    controls.transform,
                    $"m_btn_MobileItem{itemKeys[index]}",
                    $"m_text_MobileItem{itemKeys[index]}",
                    itemKeys[index],
                    584f + index * 52f,
                    514f,
                    48f,
                    48f,
                    JxqyInputIntentKind.UseItem,
                    index);
            }

            CreateActionButton(
                controls.transform,
                "m_btn_MobileLittleMap",
                "m_text_MobileLittleMap",
                "小地图\nTab",
                480f,
                514f,
                48f,
                48f,
                JxqyInputIntentKind.ToggleLittleMap);
            CreateActionButton(
                controls.transform,
                "m_btn_MobileMeditate",
                "m_text_MobileMeditate",
                "打坐\nV",
                532f,
                514f,
                48f,
                48f,
                JxqyInputIntentKind.Meditate);
        }

        private static bool AddMissingMobileButtons(Transform controls)
        {
            bool changed = EnsureBottomAnchoredActionButton(
                controls,
                "m_btn_MobileLittleMap",
                "m_text_MobileLittleMap",
                "小地图\nTab",
                new Vector2(225.7f, -260.2f),
                JxqyInputIntentKind.ToggleLittleMap);
            changed |= EnsureBottomAnchoredActionButton(
                controls,
                "m_btn_MobileMeditate",
                "m_text_MobileMeditate",
                "打坐\nV",
                new Vector2(225.7f, -321.7f),
                JxqyInputIntentKind.Meditate);
            return changed;
        }

        private static bool EnsureBottomAnchoredActionButton(
            Transform parent,
            string buttonName,
            string textName,
            string caption,
            Vector2 anchoredPosition,
            JxqyInputIntentKind intent)
        {
            if (parent.Find(buttonName) != null)
                return false;
            Button button = CreateActionButton(
                parent,
                buttonName,
                textName,
                caption,
                0f,
                0f,
                48f,
                48f,
                intent);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(48f, 48f);
            rect.localScale = Vector3.one;
            return true;
        }

        private static Button CreateActionButton(
            Transform parent,
            string buttonName,
            string textName,
            string caption,
            float left,
            float top,
            float width,
            float height,
            JxqyInputIntentKind intent,
            int slot = -1)
        {
            Button button = CreateButton(
                parent,
                buttonName,
                textName,
                caption,
                left,
                top,
                width,
                height);
            Component input = AddRuntimeComponent(
                button.gameObject,
                "Jxqy.UnityAdapters.JxqyActionButtonInput, Jxqy.Runtime");
            SetSerializedInteger(input, "_intent", (int)intent);
            SetSerializedInteger(input, "_slot", slot);
            return button;
        }

        private static Button CreateModifierButton(
            Transform parent,
            string buttonName,
            string textName,
            string caption,
            float left,
            float top,
            float width,
            float height,
            int modifier)
        {
            Button button = CreateButton(
                parent,
                buttonName,
                textName,
                caption,
                left,
                top,
                width,
                height);
            Component input = AddRuntimeComponent(
                button.gameObject,
                "Jxqy.UnityAdapters.JxqyModifierButtonInput, Jxqy.Runtime");
            SetSerializedInteger(input, "_modifier", modifier);
            return button;
        }

        private static Button CreateButton(
            Transform parent,
            string buttonName,
            string textName,
            string caption,
            float left,
            float top,
            float width,
            float height)
        {
            GameObject value = CreateUiObject(buttonName, parent);
            SetTopLeft(
                value.GetComponent<RectTransform>(),
                left,
                top,
                width,
                height);
            UIImage image = value.AddComponent<UIImage>();
            image.color = new Color(0.08f, 0.08f, 0.08f, 0.58f);
            image.raycastTarget = true;
            Button button = value.AddComponent<Button>();
            button.targetGraphic = image;
            UIText text = CreateText(textName, value.transform, caption);
            text.fontSize = caption.Length > 3 ? 13 : 20;
            return button;
        }

        private static UIText CreateText(
            string name,
            Transform parent,
            string caption)
        {
            GameObject value = CreateUiObject(name, parent);
            Stretch(value.GetComponent<RectTransform>());
            UIText text = value.AddComponent<UIText>();
            text.font = AssetDatabase.LoadAssetAtPath<UIFont>(FontPath);
            if (text.font == null)
                throw new FileNotFoundException("HUD font is missing.", FontPath);
            text.text = caption;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            Outline outline = value.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private static UIImage CreateSpriteImage(
            string name,
            Transform parent,
            string spritePath,
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
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (image.sprite == null)
                throw new FileNotFoundException(
                    "Mobile control sprite is missing.",
                    spritePath);
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.layer = LayerMask.NameToLayer("UI");
            value.transform.SetParent(parent, false);
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
                    $"Runtime component is unavailable: " +
                    assemblyQualifiedTypeName);
            }
            return target.AddComponent(type);
        }

        private static void SetSerializedReference(
            Component component,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = GetProperty(component, propertyName);
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedInteger(
            Component component,
            string propertyName,
            int value)
        {
            SerializedProperty property = GetProperty(component, propertyName);
            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedFloat(
            Component component,
            string propertyName,
            float value)
        {
            SerializedProperty property = GetProperty(component, propertyName);
            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty GetProperty(
            Component component,
            string propertyName)
        {
            var serialized = new SerializedObject(component);
            return serialized.FindProperty(propertyName) ??
                   throw new InvalidDataException(
                       $"Serialized property is missing: " +
                       $"{component.GetType().Name}.{propertyName}");
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

        private static void SetCentered(
            RectTransform rect,
            float width,
            float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
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

        private static List<string> CaptureHierarchy(GameObject root)
        {
            var paths = new List<string>();
            CaptureHierarchy(root.transform, string.Empty, paths);
            return paths;
        }

        private static void CaptureHierarchy(
            Transform parent,
            string prefix,
            List<string> paths)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == "m_go_MobileControls")
                    continue;
                string path = string.IsNullOrEmpty(prefix)
                    ? child.name
                    : $"{prefix}/{child.name}";
                paths.Add(path);
                CaptureHierarchy(child, path, paths);
            }
        }

        private static void RequireOriginalHierarchyPreserved(
            GameObject root,
            IReadOnlyList<string> expected)
        {
            List<string> actual = CaptureHierarchy(root);
            if (actual.Count != expected.Count)
            {
                throw new InvalidDataException(
                    "Installing mobile controls changed the existing HUD " +
                    "hierarchy count.");
            }
            for (int index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(
                        expected[index],
                        actual[index],
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Installing mobile controls changed the existing " +
                        $"HUD hierarchy at index {index}.");
                }
            }
        }
    }
}
