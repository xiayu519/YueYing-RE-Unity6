using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    [DisallowMultipleComponent]
    public sealed class JxqyLegacyScrollEventRelay : MonoBehaviour,
        IPointerDownHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        public JxqyLegacyVerticalScrollBinding Target { private get; set; }
        public bool IsThumb { private get; set; }
        public bool ScrollOnly { private get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!ScrollOnly)
                Target?.OnPointerDown(eventData, IsThumb);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!ScrollOnly)
                Target?.OnBeginDrag(eventData, IsThumb);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!ScrollOnly)
                Target?.OnDrag(eventData, IsThumb);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!ScrollOnly)
                Target?.OnEndDrag(eventData, IsThumb);
        }

        public void OnScroll(PointerEventData eventData) =>
            Target?.OnScroll(eventData);
    }
}
