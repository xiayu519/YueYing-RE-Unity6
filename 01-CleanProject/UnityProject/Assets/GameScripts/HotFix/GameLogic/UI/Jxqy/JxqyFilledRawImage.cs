using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// RawImage variant whose vertical fill clips geometry instead of resizing
    /// the RectTransform. Original JXQY status orbs therefore keep their native
    /// ASF dimensions at every life/thew/mana value.
    /// </summary>
    public sealed class JxqyFilledRawImage : RawImage
    {
        [SerializeField, Range(0f, 1f)]
        private float _verticalFill = 1f;

        public float VerticalFill
        {
            get => _verticalFill;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_verticalFill, clamped))
                    return;
                _verticalFill = clamped;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_verticalFill <= 0f)
                return;

            Rect rect = GetPixelAdjustedRect();
            Rect uv = uvRect;
            float visibleHeight = rect.height * _verticalFill;
            float visibleUvHeight = uv.height * _verticalFill;
            float top = rect.yMin + visibleHeight;
            float uvTop = uv.yMin + visibleUvHeight;
            Color32 vertexColor = color;

            vertexHelper.AddVert(
                new Vector3(rect.xMin, rect.yMin),
                vertexColor,
                new Vector2(uv.xMin, uv.yMin));
            vertexHelper.AddVert(
                new Vector3(rect.xMin, top),
                vertexColor,
                new Vector2(uv.xMin, uvTop));
            vertexHelper.AddVert(
                new Vector3(rect.xMax, top),
                vertexColor,
                new Vector2(uv.xMax, uvTop));
            vertexHelper.AddVert(
                new Vector3(rect.xMax, rect.yMin),
                vertexColor,
                new Vector2(uv.xMax, uv.yMin));
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }
    }
}
