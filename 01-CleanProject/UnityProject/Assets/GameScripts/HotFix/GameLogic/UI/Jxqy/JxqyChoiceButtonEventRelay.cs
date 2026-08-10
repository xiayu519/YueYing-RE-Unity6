using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class JxqyChoiceButtonEventRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private Text _label;
        private Color _normalColor;
        private Color _hoverColor;

        public void Configure(Text label, Color normalColor, Color hoverColor)
        {
            _label = label;
            _normalColor = normalColor;
            _hoverColor = hoverColor;
            ResetVisual();
        }

        public void ResetVisual() => SetColor(_normalColor);

        public void OnPointerEnter(PointerEventData eventData) =>
            SetColor(_hoverColor);

        public void OnPointerExit(PointerEventData eventData) =>
            SetColor(_normalColor);

        private void SetColor(Color color)
        {
            if (_label == null)
                return;
            _label.color = color;
            _label.canvasRenderer.SetColor(color);
            _label.canvasRenderer.SetAlpha(1f);
            _label.SetVerticesDirty();
            _label.SetMaterialDirty();
        }
    }
}
