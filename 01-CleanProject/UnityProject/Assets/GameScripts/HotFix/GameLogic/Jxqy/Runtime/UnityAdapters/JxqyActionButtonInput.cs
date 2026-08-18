using Jxqy.Domain.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyActionButtonInput : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private JxqyInputIntentKind _intent =
            JxqyInputIntentKind.PrimaryAttack;
        [SerializeField] private int _slot = -1;
        private int? _touchId;

        public void Configure(JxqyInputIntentKind intent, int slot = -1)
        {
            _intent = intent;
            _slot = slot;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (JxqyTouchInputBridge.Port?.BeginActionTouch(
                    eventData.pointerId,
                    _intent,
                    _slot) == true)
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
            JxqyTouchInputBridge.Port?.EndTouch(
                touchId,
                _intent,
                _slot);
            if (_touchId == touchId)
                _touchId = null;
        }
    }
}
