using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class JxqyMenuButtonStateRelay : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private const float AtlasFrameGapPixels = 4f;
        private RawImage _target;
        private Rect _normalUv;
        private Rect _pressedUv;
        private bool _pressed;

        public void Configure(RawImage target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _target.color = Color.white;
            _target.canvasRenderer.SetColor(Color.white);
            _target.canvasRenderer.SetAlpha(1f);
            if (_target.texture == null || _target.texture.width <= 0)
                return;
            _normalUv = _target.uvRect;
            _pressedUv = _normalUv;
            _pressedUv.x += _normalUv.width +
                            AtlasFrameGapPixels / _target.texture.width;
            if (_pressedUv.xMax > 1.0001f)
                _pressedUv = _normalUv;
            ApplyVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData?.button != PointerEventData.InputButton.Left)
                return;
            _pressed = true;
            ApplyVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pressed = false;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (_target == null)
                return;
            _target.uvRect = _pressed ? _pressedUv : _normalUv;
            _target.color = Color.white;
            _target.canvasRenderer.SetColor(Color.white);
            _target.canvasRenderer.SetAlpha(1f);
            _target.SetVerticesDirty();
            _target.SetMaterialDirty();
        }
    }
}
