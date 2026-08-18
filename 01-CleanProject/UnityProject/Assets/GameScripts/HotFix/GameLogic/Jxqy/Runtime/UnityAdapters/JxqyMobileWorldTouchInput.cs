using UnityEngine;
using UnityEngine.EventSystems;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyMobileWorldTouchInput : MonoBehaviour
    {
#if UNITY_EDITOR
        // Android/iOS Build Target 编辑器中的鼠标只模拟一根世界触摸。
        private const int EditorMouseTouchId = int.MinValue;
#endif

        private void LateUpdate()
        {
#if UNITY_ANDROID || UNITY_IOS
            JxqyTouchInputPort port = JxqyTouchInputBridge.Port;
            if (port == null)
                return;
            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                int touchId = touch.fingerId;
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        if (!port.IsClaimed(touchId) &&
                            (EventSystem.current == null ||
                             !EventSystem.current.IsPointerOverGameObject(
                                 touchId)))
                        {
                            port.BeginWorldTouch(
                                touchId,
                                JxqyTouchInputBridge.ScreenToLogical(
                                    touch.position));
                        }
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        port.MoveWorldTouch(
                            touchId,
                            JxqyTouchInputBridge.ScreenToLogical(
                                touch.position));
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        port.EndTouch(touchId);
                        break;
                }
            }
#if UNITY_EDITOR
            if (Input.touchCount == 0)
                ProcessEditorMouse(port);
#endif
#endif
        }

#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        private static void ProcessEditorMouse(JxqyTouchInputPort port)
        {
            var logicalPosition =
                JxqyTouchInputBridge.ScreenToLogical(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                if (!port.IsClaimed(EditorMouseTouchId) &&
                    (EventSystem.current == null ||
                     !EventSystem.current.IsPointerOverGameObject()))
                {
                    port.BeginWorldTouch(
                        EditorMouseTouchId,
                        logicalPosition);
                }
                return;
            }
            if (Input.GetMouseButton(0))
            {
                port.MoveWorldTouch(
                    EditorMouseTouchId,
                    logicalPosition);
                return;
            }
            if (Input.GetMouseButtonUp(0))
                port.EndTouch(EditorMouseTouchId);
        }
#endif

        private void OnDisable()
        {
            JxqyTouchInputBridge.Port?.ResetTransientState();
        }
    }
}
