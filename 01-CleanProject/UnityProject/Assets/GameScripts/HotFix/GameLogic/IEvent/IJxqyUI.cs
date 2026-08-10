using TEngine;

namespace GameLogic
{
    /// <summary>
    /// Jxqy 运行时向 TEngine UI 层发布的界面刷新事件。
    /// </summary>
    [EventInterface(EEventGroup.GroupUI)]
    public interface IJxqyUI
    {
        void OnJxqyUiChanged();
    }
}
