using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class JxqyListSlotEventRelay : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler
    {
        public JxqyListSlotWidget Target { private get; set; }

        public void OnPointerClick(PointerEventData eventData) =>
            Target?.OnPointerClick(eventData);
        public void OnPointerEnter(PointerEventData eventData) =>
            Target?.OnPointerEnter(eventData);
        public void OnPointerExit(PointerEventData eventData) =>
            Target?.OnPointerExit(eventData);
        public void OnPointerMove(PointerEventData eventData) =>
            Target?.OnPointerMove(eventData);
        public void OnPointerDown(PointerEventData eventData) =>
            Target?.OnPointerDown(eventData);
        public void OnPointerUp(PointerEventData eventData) =>
            Target?.OnPointerUp(eventData);
        public void OnBeginDrag(PointerEventData eventData) =>
            Target?.OnBeginDrag(eventData);
        public void OnDrag(PointerEventData eventData) =>
            Target?.OnDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) =>
            Target?.OnEndDrag(eventData);
        public void OnDrop(PointerEventData eventData) =>
            Target?.OnDrop(eventData);
    }
}
