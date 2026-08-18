using Jxqy.Domain.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyVirtualJoystickInput : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        [SerializeField] private RectTransform _movementArea;
        [SerializeField] private RectTransform _handle;
        [SerializeField, Min(1)] private float _radius = 80f;
        private int? _touchId;

        public void Configure(
            RectTransform movementArea,
            RectTransform handle,
            float radius)
        {
            _movementArea = movementArea;
            _handle = handle;
            _radius = Mathf.Max(1f, radius);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (JxqyTouchInputBridge.Port?.BeginMovementTouch(
                    eventData.pointerId) == true)
            {
                _touchId = eventData.pointerId;
                UpdateMove(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateMove(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release(eventData.pointerId);
        }

        private void OnDisable()
        {
            if (_touchId.HasValue)
                Release(_touchId.Value);
        }

        private void UpdateMove(PointerEventData eventData)
        {
            RectTransform area = _movementArea != null
                ? _movementArea
                : transform as RectTransform;
            if (area == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local))
            {
                return;
            }
            Vector2 clamped = Vector2.ClampMagnitude(local, _radius);
            if (_handle != null)
                _handle.anchoredPosition = clamped;
            Vector2 direction = clamped / Mathf.Max(1, _radius);
            JxqyTouchInputBridge.Port?.SetVirtualMove(
                eventData.pointerId,
                new JxqyFloat2(direction.x, direction.y));
        }

        private void Release(int touchId)
        {
            JxqyTouchInputBridge.Port?.EndTouch(touchId);
            if (_touchId == touchId)
                _touchId = null;
            if (_handle != null)
                _handle.anchoredPosition = Vector2.zero;
        }
    }
}
