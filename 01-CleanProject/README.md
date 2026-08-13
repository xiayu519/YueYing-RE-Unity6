# 月影剑侠 Unity 6 干净工程

Unity 版本固定为 `6000.5.4f1`。

## 首次运行

1. 使用 Unity Hub 打开 `UnityProject`。
2. 将 `JxqyResources-20260810.unitypackage` 完整导入工程。
3. 再导入 [`JxqyResources-Patch-20260812-HuanXianZhiRen.unitypackage`](https://github.com/xiayu519/YueYing-RE-Unity6/releases/download/windows-il2cpp-20260810/JxqyResources-Patch-20260812-HuanXianZhiRen.unitypackage)，并确认覆盖同名文件。已经导入基础资源包的工程只需导入此补丁。
4. 等待 Unity 完成导入和编译。
5. 双击根目录的 `SetupResources.bat`。
6. 看到 `SUCCESS` 后，打开 `Assets/Scenes/main.unity` 并点击 Play。

脚本会检查资源完整性、启用 YooAsset `EditorSimulateMode`，并刷新 `DefaultPackage` 与 `JxqyPackage` 的编辑器模拟清单。模拟清单使用虚拟 Bundle，不会构建真实 AssetBundle、不会生成 `StreamingAssets`，也不会打包玩家程序。
