using Jxqy.Ports;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyModifierButtonInput : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private JxqyInputButtons _modifier =
            JxqyInputButtons.RunModifier;
        private int? _touchId;

        public void Configure(JxqyInputButtons modifier)
        {
            _modifier = modifier;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (JxqyTouchInputBridge.Port?.BeginModifierTouch(
                    eventData.pointerId,
                    _modifier) == true)
            {
                _touchId = eventData.pointerId;
            }
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

        private void Release(int touchId)
        {
            JxqyTouchInputBridge.Port?.EndModifierTouch(
                touchId,
                _modifier);
            if (_touchId == touchId)
                _touchId = null;
        }
    }
}
