using System;
using System.Collections.Generic;
using Jxqy.Domain.Input;
using Jxqy.Domain.World;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    [CreateAssetMenu(
        fileName = "JxqyMobileControlLayout",
        menuName = "Jxqy/Input/Mobile Control Layout")]
    public sealed class JxqyMobileControlLayoutAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public JxqyMobileControlKind Kind;
            public Vector2 Center;
            public Vector2 Size = new Vector2(72, 72);
            [Range(0, 1)] public float Opacity = 0.65f;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public JxqyMobileControlLayout Build()
        {
            if (_entries.Count == 0)
                return JxqyMobileControlLayout.CreateDefaultLandscape();
            var values = new List<JxqyMobileControlPlacement>(
                _entries.Count);
            foreach (Entry entry in _entries)
            {
                values.Add(new JxqyMobileControlPlacement(
                    entry.Kind,
                    new JxqyFloat2(entry.Center.x, entry.Center.y),
                    new JxqyFloat2(entry.Size.x, entry.Size.y),
                    entry.Opacity));
            }
            return new JxqyMobileControlLayout(values);
        }
    }

    public sealed class JxqyMobileControlLayoutView : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public JxqyMobileControlKind Kind;
            public RectTransform Rect;
            public CanvasGroup CanvasGroup;
        }

        [SerializeField] private JxqyMobileControlLayoutAsset _layout;
        [SerializeField] private List<Binding> _bindings =
            new List<Binding>();

        private void OnEnable()
        {
            Apply();
        }

        public void Apply()
        {
            JxqyMobileControlLayout layout = _layout != null
                ? _layout.Build()
                : JxqyMobileControlLayout.CreateDefaultLandscape();
            foreach (Binding binding in _bindings)
            {
                if (binding?.Rect == null)
                    continue;
                JxqyMobileControlPlacement placement =
                    layout[binding.Kind];
                RectTransform rect = binding.Rect;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    placement.Center.X,
                    -placement.Center.Y);
                rect.sizeDelta = new Vector2(
                    placement.Size.X,
                    placement.Size.Y);
                CanvasGroup group = binding.CanvasGroup != null
                    ? binding.CanvasGroup
                    : rect.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = placement.Opacity;
            }
        }
    }
}
