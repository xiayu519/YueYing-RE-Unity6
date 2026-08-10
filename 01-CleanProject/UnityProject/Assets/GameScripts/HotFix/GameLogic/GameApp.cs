using System.Collections.Generic;
using System.Reflection;
using GameLogic;
using Jxqy.Bootstrap;
#if ENABLE_OBFUZ
using Obfuz;
#endif
using TEngine;
#pragma warning disable CS0436


/// <summary>
/// 游戏App。
/// </summary>
#if ENABLE_OBFUZ
[ObfuzIgnore(ObfuzScope.TypeName | ObfuzScope.MethodName)]
#endif
public partial class GameApp
{
    private static List<Assembly> _hotfixAssembly;
    private static JxqyUiRouter _jxqyUiRouter;

    /// <summary>
    /// 热更域App主入口。
    /// </summary>
    /// <param name="objects"></param>
    public static void Entrance(object[] objects)
    {
        GameEvent.EventMgr.Init();
        GameEventHelper.Init();
        _hotfixAssembly = (List<Assembly>)objects[0];
        Log.Warning("======= 看到此条日志代表你成功运行了热更新代码 =======");
        Log.Warning("======= Entrance GameApp =======");
        Utility.Unity.AddDestroyListener(Release);
        Log.Warning("======= StartGameLogic =======");
        StartGameLogic();
    }
    
    private static void StartGameLogic()
    {
        JxqyGameBootstrap.UiSessionReady += OnJxqyUiSessionReady;
        JxqyGameBootstrap.Start();
    }

    private static void OnJxqyUiSessionReady(
        Jxqy.Domain.Presentation.JxqyUiSession session)
    {
        _jxqyUiRouter?.Dispose();
        _jxqyUiRouter = new JxqyUiRouter(session);
        _jxqyUiRouter.Start();
    }
    
    private static void Release()
    {
        JxqyGameBootstrap.UiSessionReady -= OnJxqyUiSessionReady;
        _jxqyUiRouter?.Dispose();
        _jxqyUiRouter = null;
        JxqyGameBootstrap.Shutdown();
        SingletonSystem.Release();
        GameModule.Shutdown();
        GameEvent.EventMgr.Init();
        Log.Warning("======= Release GameApp =======");
    }
}
