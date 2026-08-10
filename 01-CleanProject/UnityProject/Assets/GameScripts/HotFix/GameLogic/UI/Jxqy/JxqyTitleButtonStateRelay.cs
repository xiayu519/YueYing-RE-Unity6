using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// Reproduces the original title button's two-frame TrackBtn behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JxqyTitleButtonStateRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private const float AtlasFrameGapPixels = 4f;
        private RawImage _target;
        private Rect _normalUv;
        private Rect _highlightedUv;
        private bool _pointerInside;

        public void Configure(RawImage target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            if (_target.texture == null || _target.texture.width <= 0)
                throw new ArgumentException("Title button atlas is missing.", nameof(target));
            _normalUv = _target.uvRect;
            _highlightedUv = _normalUv;
            _highlightedUv.x += _normalUv.width +
                                AtlasFrameGapPixels / _target.texture.width;
            if (_highlightedUv.xMax > 1.0001f)
                throw new ArgumentException(
                    "Title button atlas does not contain its hover frame.",
                    nameof(target));
            ApplyVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_pointerInside)
                return;
            _pointerInside = true;
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (_target == null)
                return;
            _target.uvRect = _pointerInside ? _highlightedUv : _normalUv;
            _target.color = Color.white;
            _target.canvasRenderer.SetColor(Color.white);
            _target.canvasRenderer.SetAlpha(1f);
            _target.SetVerticesDirty();
            _target.SetMaterialDirty();
        }
    }
}
