using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class JxqyPointerHoldRelay : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        private bool _held;
        private int _pressedFrame;

        public Action Pressed { private get; set; }
        public Action Held { private get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            _held = true;
            _pressedFrame = Time.frameCount;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _held = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _held = false;
        }

        private void Update()
        {
            if (_held && Time.frameCount != _pressedFrame)
                Held?.Invoke();
        }
    }
}
