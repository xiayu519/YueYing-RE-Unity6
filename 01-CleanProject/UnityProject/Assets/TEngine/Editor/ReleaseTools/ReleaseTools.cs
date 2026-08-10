using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using BuildResult = UnityEditor.Build.Reporting.BuildResult;

namespace TEngine
{
    /// <summary>
    /// 打包工具类。
    /// <remarks>通过CommandLineReader可以不前台开启Unity实现静默打包以及CLI工作流，详见CommandLineReader.cs example1</remarks>
    /// </summary>
    public static class ReleaseTools
    {
        #region CLI 入口

        public static void BuildDll()
        {
            string platform = CommandLineReader.GetCustomArgument("platform");
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError($"Build Asset Bundle Error！platform is null");
                return;
            }

            BuildTarget target = GetBuildTarget(platform);

            // BuildDLLCommand.BuildAndCopyDlls(target);
        }

        public static void BuildAssetBundle()
        {
            string outputRoot = CommandLineReader.GetCustomArgument("outputRoot");
            if (string.IsNullOrEmpty(outputRoot))
            {
                Debug.LogError($"Build Asset Bundle Error！outputRoot is null");
                return;
            }

            string packageVersion = CommandLineReader.GetCustomArgument("packageVersion");
            if (string.IsNullOrEmpty(packageVersion))
            {
                Debug.LogError($"Build Asset Bundle Error！packageVersion is null");
                return;
            }

            string platform = CommandLineReader.GetCustomArgument("platform");
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError($"Build Asset Bundle Error！platform is null");
                return;
            }

            BuildTarget target = GetBuildTarget(platform);
            BuildInternal(target, outputRoot);
            Debug.LogWarning($"Start BuildPackage BuildTarget:{target} outputPath:{outputRoot}");
        }

        #endregion

        #region MenuItem 入口（兼容原有菜单）

        [MenuItem("TEngine/Build/一键打包AssetBundle _F8")]
        public static void BuildCurrentPlatformAB()
        {
            var config = BuildConfig.CreateDefault();
            config.BuildHotFixDll = true;
            BuildWithConfig(config, buildPlayer: false);
        }

        [MenuItem("TEngine/Build/一键打包Window", false, 30)]
        public static void AutomationBuild()
        {
            var config = BuildConfig.CreateDefault();
            config.BuildTarget = BuildTarget.StandaloneWindows64;
            config.OutputRoot = Application.dataPath + "/../Builds/Windows";
            config.BuildPlayer = true;
            config.PlayerPlatform = BuildTarget.StandaloneWindows64;
            config.PlayerOutputPath = $"{Application.dataPath}/../Build/Windows/Release_Windows.exe";
            BuildWithConfig(config, buildPlayer: true);
        }

        [MenuItem("TEngine/Build/一键打包Android", false, 30)]
        public static void AutomationBuildAndroid()
        {
            var config = BuildConfig.CreateDefault();
            config.BuildTarget = BuildTarget.Android;
            config.OutputRoot = Application.dataPath + "/../Bundles";
            config.BuildPlayer = true;
            config.PlayerPlatform = BuildTarget.Android;
            config.PlayerOutputPath = $"{Application.dataPath}/../Build/Android/{BuildConfig.GetDefaultPackageVersion()}Android.apk";
            BuildWithConfig(config, buildPlayer: true);
        }

        [MenuItem("TEngine/Build/一键打包IOS", false, 30)]
        public static void AutomationBuildIOS()
        {
            var config = BuildConfig.CreateDefault();
            config.BuildTarget = BuildTarget.iOS;
            config.OutputRoot = Application.dataPath + "/../Bundles";
            config.BuildPlayer = true;
            config.PlayerPlatform = BuildTarget.iOS;
            config.PlayerOutputPath = $"{Application.dataPath}/../Build/IOS/XCode_Project";
            BuildWithConfig(config, buildPlayer: true);
        }

        #endregion

        #region 参数化构建入口

        /// <summary>
        /// 通过 BuildConfig 执行完整构建流程
        /// </summary>
        public static void BuildWithConfig(BuildConfig config, bool buildPlayer)
        {
            var buildResult = BuildAssetPackage(config);
            if (!buildResult.Success)
            {
                Debug.LogError($"[BuildWithConfig] AssetBundle构建失败: {buildResult.ErrorInfo}");
                return;
            }

            Debug.Log($"[BuildWithConfig] AssetBundle构建成功: {buildResult.OutputPackageDirectory}");

            // 4. [最小包] 删除 StreamingAssets 中的 .bundle 文件
            if (config.MinimalPackage)
            {
                ProcessMinimalPackage(config.PackageVersion, config.RetainTags, buildResult.OutputPackageDirectory);
            }

            // 5. 刷新资源
            AssetDatabase.Refresh();

            // 7. [可选] 构建 Player
            if (buildPlayer || config.BuildPlayer)
            {
                BuildImp(
                    BuildConfig.GetBuildTargetGroup(config.PlayerPlatform),
                    config.PlayerPlatform,
                    config.PlayerOutputPath
                );
            }
        }

        /// <summary>
        /// 构建单个 YooAsset 包并返回可供自动化验收使用的结果。
        /// </summary>
        public static YooAsset.Editor.BuildResult BuildAssetPackage(
            BuildConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.PackageName))
                throw new ArgumentException(
                    "Package name is empty.",
                    nameof(config));
            if (config.BuildHotFixDll)
            {
                throw new NotSupportedException(
                    "The clean Unity 6 project is pure IL2CPP and does not build HybridCLR DLL payloads.");
            }
            AssetDatabase.Refresh();
            return BuildInternalWithConfig(config);
        }

        /// <summary>
        /// 构建可交付的内置资源包，并以不暴露 YooAsset Editor
        /// 类型的摘要返回结果，供独立的验收程序集调用。
        /// </summary>
        public static AssetPackageBuildSummary BuildDeliveryAssetPackage(
            string packageName,
            string packageVersion,
            BuildTarget buildTarget,
            string outputRoot,
            bool buildHotFixDll)
        {
            var config = new BuildConfig
            {
                PackageName = packageName,
                PackageVersion = packageVersion,
                BuildTarget = buildTarget,
                PlayerPlatform = buildTarget,
                OutputRoot = outputRoot,
                // The migrated JXQY package contains tens of thousands of
                // original assets. Unity 6's SBP accumulates the complete
                // WriteSerializedFiles graph for this package and can exhaust
                // native memory before it writes the first bundle. YooAsset's
                // builtin pipeline preserves the same package/address manifest
                // while serializing the bundles incrementally.
                BuildPipeline = string.Equals(
                    packageName,
                    "JxqyPackage",
                    StringComparison.Ordinal)
                        ? EBuildPipeline.BuiltinBuildPipeline
                        : EBuildPipeline.ScriptableBuildPipeline,
                CompressOption = ECompressOption.LZ4,
                EncryptionType =
                    GetEncryptionTypeFromResourceModuleDriver(),
                BuildinFileCopyOption =
                    EBuildinFileCopyOption.ClearAndCopyAll,
                VerifyBuildingResult = true,
                EnableSharePackRule =
                    !string.Equals(
                        packageName,
                        "JxqyPackage",
                        StringComparison.Ordinal),
                UseAssetDependencyDB =
                    !string.Equals(
                        packageName,
                        "JxqyPackage",
                        StringComparison.Ordinal),
                BuildHotFixDll = buildHotFixDll,
                BuildPlayer = false,
                MinimalPackage = false,
            };
            YooAsset.Editor.BuildResult result =
                BuildAssetPackage(config);
            return new AssetPackageBuildSummary
            {
                PackageName = packageName,
                PackageVersion = packageVersion,
                Success = result.Success,
                OutputPackageDirectory =
                    result.OutputPackageDirectory ?? string.Empty,
                ErrorInfo = result.ErrorInfo ?? string.Empty,
            };
        }

        #endregion

        #region AssetBundle 构建

        private static YooAsset.Editor.BuildResult BuildInternalWithConfig(BuildConfig config)
        {
            Debug.Log($"开始构建 : {config.BuildTarget}");

            IBuildPipeline pipeline;
            BuildParameters buildParameters;

            if (config.BuildPipeline == EBuildPipeline.BuiltinBuildPipeline)
            {
                var builtinBuildParameters = new BuiltinBuildParameters();
                pipeline = new BuiltinBuildPipeline();
                buildParameters = builtinBuildParameters;
                builtinBuildParameters.CompressOption = config.CompressOption;
            }
            else
            {
                var scriptableBuildParameters = new ScriptableBuildParameters();
                pipeline = new ScriptableBuildPipeline();
                buildParameters = scriptableBuildParameters;
                scriptableBuildParameters.CompressOption = config.CompressOption;
                scriptableBuildParameters.BuiltinShadersBundleName =
                    GetBuiltinShaderBundleName(config.PackageName);
                scriptableBuildParameters.ReplaceAssetPathWithAddress = Settings.UpdateSetting.GetReplaceAssetPathWithAddress();
            }

            string outputRoot = config.OutputRoot;
            if (!Path.IsPathRooted(outputRoot))
            {
                outputRoot = Path.Combine(Application.dataPath + "/../", outputRoot);
                outputRoot = Path.GetFullPath(outputRoot).Replace('\\', '/');
            }

            buildParameters.BuildOutputRoot = outputRoot;
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = config.BuildPipeline.ToString();
            buildParameters.BuildTarget = config.BuildTarget;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.PackageName = config.PackageName;
            buildParameters.PackageVersion = config.PackageVersion;
            buildParameters.VerifyBuildingResult = config.VerifyBuildingResult;
            buildParameters.EnableSharePackRule = config.EnableSharePackRule;
            buildParameters.FileNameStyle = config.FileNameStyle;
            buildParameters.BuildinFileCopyOption = config.BuildinFileCopyOption;
            buildParameters.BuildinFileCopyParams = string.Empty;
            buildParameters.EncryptionServices = GetEncryptionFromType(config.EncryptionType);
            buildParameters.ClearBuildCacheFiles = config.ClearBuildCache;
            buildParameters.UseAssetDependencyDB = config.UseAssetDependencyDB;

            var result = pipeline.Run(buildParameters, true);
            return result;
        }

        /// <summary>
        /// 旧版 BuildInternal，供 CLI 入口兼容
        /// </summary>
        private static void BuildInternal(BuildTarget buildTarget, string outputRoot, string packageVersion = "1.0",
            EBuildPipeline buildPipeline = EBuildPipeline.ScriptableBuildPipeline)
        {
            Debug.Log($"开始构建 : {buildTarget}");

            IBuildPipeline pipeline = null;
            BuildParameters buildParameters = null;

            if (buildPipeline == EBuildPipeline.BuiltinBuildPipeline)
            {
                BuiltinBuildParameters builtinBuildParameters = new BuiltinBuildParameters();
                pipeline = new BuiltinBuildPipeline();
                buildParameters = builtinBuildParameters;
                builtinBuildParameters.CompressOption = ECompressOption.LZ4;
            }
            else
            {
                ScriptableBuildParameters scriptableBuildParameters = new ScriptableBuildParameters();
                pipeline = new ScriptableBuildPipeline();
                buildParameters = scriptableBuildParameters;
                scriptableBuildParameters.CompressOption = ECompressOption.LZ4;
                scriptableBuildParameters.BuiltinShadersBundleName = GetBuiltinShaderBundleName("DefaultPackage");
                scriptableBuildParameters.ReplaceAssetPathWithAddress = Settings.UpdateSetting.GetReplaceAssetPathWithAddress();
            }

            buildParameters.BuildOutputRoot = outputRoot;
            buildParameters.BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            buildParameters.BuildPipeline = buildPipeline.ToString();
            buildParameters.BuildTarget = buildTarget;
            buildParameters.BuildBundleType = (int)EBuildBundleType.AssetBundle;
            buildParameters.PackageName = "DefaultPackage";
            buildParameters.PackageVersion = packageVersion;
            buildParameters.VerifyBuildingResult = true;
            buildParameters.EnableSharePackRule = true;
            buildParameters.FileNameStyle = EFileNameStyle.BundleName_HashName;
            buildParameters.BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll;
            buildParameters.BuildinFileCopyParams = string.Empty;
            buildParameters.EncryptionServices = GetEncryptionFromResourceModuleDriver();
            buildParameters.ClearBuildCacheFiles = false;
            buildParameters.UseAssetDependencyDB = true;

            var buildResult = pipeline.Run(buildParameters, true);
            if (buildResult.Success)
            {
                Debug.Log($"构建成功 : {buildResult.OutputPackageDirectory}");
            }
            else
            {
                Debug.LogError($"构建失败 : {buildResult.ErrorInfo}");
            }
        }

        #endregion

        #region 最小包后处理
        /// <summary>
        /// 读取文件的文本数据
        /// </summary>
        public static string ReadAllText(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return null;
            }
            return File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// 最小包模式：删除 StreamingAssets 中不带保留 tag 的 .bundle 文件
        /// 使用构建输出的 BuildReport（JSON）获取 bundle 的 tag 信息
        /// </summary>
        public static void ProcessMinimalPackage(string packageVersion, string retainTags, string outputPackageDirectory)
        {
            string streamingRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot();
            string packageName = "DefaultPackage";

            // 定位构建报告文件
            string reportFileName = YooAssetSettingsData.GetBuildReportFileName(packageName, packageVersion);
            string reportPath = $"{outputPackageDirectory}/{reportFileName}";

            if (!File.Exists(reportPath))
            {
                Debug.LogError($"[最小包] 未找到构建报告: {reportPath}，跳过最小包处理");
                return;
            }

            // 反序列化 BuildReport
            YooAsset.Editor.BuildReport buildReport;
            try
            {
                string jsonData = ReadAllText(reportPath);
                buildReport = YooAsset.Editor.BuildReport.Deserialize(jsonData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[最小包] 反序列化构建报告失败: {e.Message}");
                return;
            }

            // 构建保留文件名集合
            HashSet<string> retainFileNames = new HashSet<string>();
            string[] retainTagArray = ParseRetainTags(retainTags);

            if (retainTagArray.Length > 0)
            {
                foreach (var bundleInfo in buildReport.BundleInfos)
                {
                    if (bundleInfo.Tags != null && HasTag(bundleInfo.Tags, retainTagArray))
                    {
                        retainFileNames.Add(bundleInfo.FileName);
                    }
                }
                Debug.Log($"[最小包] 保留 Tag: [{string.Join(", ", retainTagArray)}]，匹配 {retainFileNames.Count} 个 bundle");
            }

            // 扫描 StreamingAssets 下的 .bundle 文件
            if (!Directory.Exists(streamingRoot))
            {
                Debug.LogWarning($"[最小包] StreamingAssets 目录不存在: {streamingRoot}");
                return;
            }

            string[] bundleFiles = Directory.GetFiles(streamingRoot, "*.bundle", SearchOption.AllDirectories);
            int deletedCount = 0;
            int retainedCount = 0;

            foreach (var file in bundleFiles)
            {
                string fileName = Path.GetFileName(file);
                if (retainFileNames.Contains(fileName))
                {
                    retainedCount++;
                    Debug.Log($"[最小包] 保留: {fileName}");
                }
                else
                {
                    File.Delete(file);
                    deletedCount++;
                    Debug.Log($"[最小包] 删除: {fileName}");
                }
            }

            Debug.Log($"[最小包] 处理完成 - 删除 {deletedCount} 个 .bundle，保留 {retainedCount} 个 .bundle");

            // 删除空目录
            CleanEmptyDirectories(streamingRoot);
        }

        private static bool HasTag(string[] bundleTags, string[] matchTags)
        {
            foreach (var matchTag in matchTags)
            {
                foreach (var bundleTag in bundleTags)
                {
                    if (bundleTag == matchTag)
                        return true;
                }
            }
            return false;
        }

        private static string[] ParseRetainTags(string retainTags)
        {
            if (string.IsNullOrWhiteSpace(retainTags))
                return Array.Empty<string>();

            return retainTags
                .Split(',', '，') // 支持中英文逗号
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
        }

        private static void CleanEmptyDirectories(string rootPath)
        {
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                CleanEmptyDirectories(dir);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
        }

        #endregion

        #region Player 构建

        public static UnityEditor.Build.Reporting.BuildReport BuildImp(
            BuildTargetGroup buildTargetGroup,
            BuildTarget buildTarget,
            string locationPathName)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
            AssetDatabase.Refresh();

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray(),
                locationPathName = locationPathName,
                targetGroup = buildTargetGroup,
                target = buildTarget,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build success: {summary.totalSize / 1024 / 1024} MB, {summary.outputPath}");
            }
            else
            {
                Debug.Log($"Build Failed" + summary.result);
            }
            return report;
        }

        #endregion

        #region 工具方法

        private static BuildTarget GetBuildTarget(string platform)
        {
            BuildTarget target = BuildTarget.NoTarget;
            switch (platform)
            {
                case "Android":
                    target = BuildTarget.Android;
                    break;
                case "IOS":
                    target = BuildTarget.iOS;
                    break;
                case "Windows":
                    target = BuildTarget.StandaloneWindows64;
                    break;
                case "MacOS":
                    target = BuildTarget.StandaloneOSX;
                    break;
                case "Linux":
                    target = BuildTarget.StandaloneLinux64;
                    break;
                case "WebGL":
                    target = BuildTarget.WebGL;
                    break;
                case "Switch":
                    target = BuildTarget.Switch;
                    break;
                case "PS4":
                    target = BuildTarget.PS4;
                    break;
                case "PS5":
                    target = BuildTarget.PS5;
                    break;
            }

            return target;
        }

        private static string GetBuiltinShaderBundleName(string packageName)
        {
            var uniqueBundleName = AssetBundleCollectorSettingData.Setting.UniqueBundleName;
            var packRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
            return packRuleResult.GetBundleName(packageName, uniqueBundleName);
        }

        /// <summary>
        /// 根据 EncryptionType 枚举获取加密服务
        /// </summary>
        private static IEncryptionServices GetEncryptionFromType(EncryptionType encryptionType)
        {
            return encryptionType switch
            {
                EncryptionType.FileOffSet => new FileOffsetEncryption(),
                EncryptionType.FileStream => new FileStreamEncryption(),
                _ => null
            };
        }

        /// <summary>
        /// 根据 ResourceModuleDriver 的 encryptionType 获取对应的加密服务（旧版兼容）
        /// </summary>
        private static IEncryptionServices GetEncryptionFromResourceModuleDriver()
        {
            return GetEncryptionFromType(
                GetEncryptionTypeFromResourceModuleDriver());
        }

        private static EncryptionType GetEncryptionTypeFromResourceModuleDriver()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab GameEntry");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[BuildInternal] Failed to find GameEntry.prefab");
                return EncryptionType.None;
            }

            var gameEntryPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var gameEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gameEntryPath);
            if (gameEntryPrefab == null)
            {
                Debug.LogWarning("[BuildInternal] Failed to load GameEntry.prefab");
                return EncryptionType.None;
            }

            var resourceModuleDriver = gameEntryPrefab.GetComponentInChildren<ResourceModuleDriver>();
            if (resourceModuleDriver == null)
            {
                Debug.LogWarning("[BuildInternal] ResourceModuleDriver not found in GameEntry.prefab");
                return EncryptionType.None;
            }

            var encryptionType = resourceModuleDriver.EncryptionType;
            Debug.Log($"[BuildInternal] Use EncryptionType from ResourceModuleDriver: {encryptionType}");
            return encryptionType;
        }

        private static string GetBuildPackageVersion()
        {
            int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
        }

        #endregion
    }

    public sealed class AssetPackageBuildSummary
    {
        public string PackageName = string.Empty;
        public string PackageVersion = string.Empty;
        public bool Success;
        public string OutputPackageDirectory = string.Empty;
        public string ErrorInfo = string.Empty;
    }
}
