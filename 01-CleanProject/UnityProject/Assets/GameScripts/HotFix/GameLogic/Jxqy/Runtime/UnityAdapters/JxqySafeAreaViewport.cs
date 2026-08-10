using Jxqy.Domain.Presentation;
using Jxqy.Domain.World;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqySafeAreaViewport : MonoBehaviour
    {
        [SerializeField]
        private Camera targetCamera;

        [SerializeField]
        private RectTransform logicalCanvasContainer;

        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        public JxqyViewportLayout Layout { get; private set; }

        public void Initialize(
            Camera camera,
            RectTransform canvasContainer = null)
        {
            targetCamera = camera;
            logicalCanvasContainer = canvasContainer;
            Refresh(true);
        }

        private void Update()
        {
            Refresh(false);
        }

        public void Refresh(bool force)
        {
            if (targetCamera == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
                return;
            Rect safe = Screen.safeArea;
            if (!force &&
                safe == _lastSafeArea &&
                Screen.width == _lastWidth &&
                Screen.height == _lastHeight)
                return;
            _lastSafeArea = safe;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            Layout = JxqyLogicalViewport.Calculate(
                Screen.width,
                Screen.height,
                new JxqyIntRect(
                    Mathf.RoundToInt(safe.x),
                    Mathf.RoundToInt(safe.y),
                    Mathf.RoundToInt(safe.width),
                    Mathf.RoundToInt(safe.height)));
            JxqyIntRect pixel = Layout.PixelRect;
            var normalizedRect = new Rect(
                pixel.X / (float)Screen.width,
                pixel.Y / (float)Screen.height,
                pixel.Width / (float)Screen.width,
                pixel.Height / (float)Screen.height);
            targetCamera.rect = normalizedRect;
            if (logicalCanvasContainer == null)
                return;
            logicalCanvasContainer.anchorMin =
                new Vector2(normalizedRect.xMin, normalizedRect.yMin);
            logicalCanvasContainer.anchorMax =
                new Vector2(normalizedRect.xMax, normalizedRect.yMax);
            logicalCanvasContainer.offsetMin = Vector2.zero;
            logicalCanvasContainer.offsetMax = Vector2.zero;
        }
    }
}
