using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YooAsset.Editor
{
    [Serializable]
    public class AssetBundleCollector
    {
        private const string StopDependencyTraversalAtCollectorRoot =
            "StopDependencyTraversalAtCollectorRoot";

        [NonSerialized]
        private List<string> _dependencyPathStack;

        [NonSerialized]
        private HashSet<string> _visitedDependencyPaths;

        [NonSerialized]
        private List<string> _sceneDependencyPaths;

        [NonSerialized]
        private HashSet<string> _sceneDependencyGuids;

        /// <summary>
        /// 收集路径
        /// 注意：支持文件夹或单个资源文件
        /// </summary>
        public string CollectPath = string.Empty;

        /// <summary>
        /// 收集器的GUID
        /// </summary>
        public string CollectorGUID = string.Empty;

        /// <summary>
        /// 收集器类型
        /// </summary>
        public ECollectorType CollectorType = ECollectorType.MainAssetCollector;

        /// <summary>
        /// 寻址规则类名
        /// </summary>
        public string AddressRuleName = nameof(AddressByFileName);

        /// <summary>
        /// 打包规则类名
        /// </summary>
        public string PackRuleName = nameof(PackDirectory);

        /// <summary>
        /// 过滤规则类名
        /// </summary>
        public string FilterRuleName = nameof(CollectAll);

        /// <summary>
        /// 资源分类标签
        /// </summary>
        public string AssetTags = string.Empty;

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        public string UserData = string.Empty;


        /// <summary>
        /// 收集器是否有效
        /// </summary>
        public bool IsValid()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CollectPath) == null)
                return false;

            if (CollectorType == ECollectorType.None)
                return false;

            if (AssetBundleCollectorSettingData.HasAddressRuleName(AddressRuleName) == false)
                return false;

            if (AssetBundleCollectorSettingData.HasPackRuleName(PackRuleName) == false)
                return false;

            if (AssetBundleCollectorSettingData.HasFilterRuleName(FilterRuleName) == false)
                return false;

            return true;
        }

        /// <summary>
        /// 检测配置错误
        /// </summary>
        public void CheckConfigError()
        {
            string assetGUID = AssetDatabase.AssetPathToGUID(CollectPath);
            if (string.IsNullOrEmpty(assetGUID))
                throw new Exception($"Invalid collect path : {CollectPath}");

            if (CollectorType == ECollectorType.None)
                throw new Exception($"{nameof(ECollectorType)}.{ECollectorType.None} is invalid in collector : {CollectPath}");

            if (AssetBundleCollectorSettingData.HasPackRuleName(PackRuleName) == false)
                throw new Exception($"Invalid {nameof(IPackRule)} class type : {PackRuleName} in collector : {CollectPath}");

            if (AssetBundleCollectorSettingData.HasFilterRuleName(FilterRuleName) == false)
                throw new Exception($"Invalid {nameof(IFilterRule)} class type : {FilterRuleName} in collector : {CollectPath}");

            if (AssetBundleCollectorSettingData.HasAddressRuleName(AddressRuleName) == false)
                throw new Exception($"Invalid {nameof(IAddressRule)} class type : {AddressRuleName} in collector : {CollectPath}");
        }

        /// <summary>
        /// 修复配置错误
        /// </summary>
        public bool FixConfigError()
        {
            bool isFixed = false;

            if (string.IsNullOrEmpty(CollectorGUID) == false)
            {
                string convertAssetPath = AssetDatabase.GUIDToAssetPath(CollectorGUID);
                if (string.IsNullOrEmpty(convertAssetPath))
                {
                    Debug.LogWarning($"Collector GUID {CollectorGUID} is invalid and has been auto removed !");
                    CollectorGUID = string.Empty;
                    isFixed = true;
                }
                else
                {
                    if (CollectPath != convertAssetPath)
                    {
                        CollectPath = convertAssetPath;
                        isFixed = true;
                        Debug.LogWarning($"Fix collect path : {CollectPath} -> {convertAssetPath}");
                    }
                }
            }

            /*
            string convertGUID = AssetDatabase.AssetPathToGUID(CollectPath);
            if(string.IsNullOrEmpty(convertGUID) == false)
            {
                CollectorGUID = convertGUID;
            }
            */

            return isFixed;
        }

        /// <summary>
        /// 获取打包收集的资源文件
        /// </summary>
        public List<CollectAssetInfo> GetAllCollectAssets(CollectCommand command, AssetBundleCollectorGroup group)
        {
            bool ignoreStaticCollector = command.IsFlagSet(ECollectFlags.IgnoreStaticCollector);
            if (ignoreStaticCollector)
            {
                if (CollectorType == ECollectorType.StaticAssetCollector)
                    return new List<CollectAssetInfo>();
            }

            bool ignoreDependCollector = command.IsFlagSet(ECollectFlags.IgnoreDependCollector);
            if (ignoreDependCollector)
            {
                if (CollectorType == ECollectorType.DependAssetCollector)
                    return new List<CollectAssetInfo>();
            }

            Dictionary<string, CollectAssetInfo> result = new Dictionary<string, CollectAssetInfo>(1000);

            // 收集打包资源路径
            List<string> findAssets = new List<string>();
            if (AssetDatabase.IsValidFolder(CollectPath))
            {
                IFilterRule filterRuleInstance = AssetBundleCollectorSettingData.GetFilterRuleInstance(FilterRuleName);
                string findAssetType = filterRuleInstance.FindAssetType;
                string searchFolder = CollectPath;
                string[] findResult = EditorTools.FindAssets(findAssetType, searchFolder);
                findAssets.AddRange(findResult);
            }
            else
            {
                string assetPath = CollectPath;
                findAssets.Add(assetPath);
            }

            // 收集打包资源信息
            foreach (string assetPath in findAssets)
            {
                var assetInfo = new AssetInfo(assetPath);
                if (command.IgnoreRule.IsIgnore(assetInfo) == false && IsCollectAsset(group, assetInfo))
                {
                    if (result.ContainsKey(assetPath) == false)
                    {
                        var collectAssetInfo = CreateCollectAssetInfo(command, group, assetInfo);
                        result.Add(assetPath, collectAssetInfo);
                    }
                    else
                    {
                        throw new Exception($"The collecting asset file is existed : {assetPath} in collector : {CollectPath}");
                    }
                }
            }

            // 检测可寻址地址是否重复
            if (command.EnableAddressable)
            {
                var addressTemper = new Dictionary<string, string>();
                foreach (var collectInfoPair in result)
                {
                    if (collectInfoPair.Value.CollectorType == ECollectorType.MainAssetCollector)
                    {
                        string address = collectInfoPair.Value.Address;
                        string assetPath = collectInfoPair.Value.AssetInfo.AssetPath;
                        if (string.IsNullOrEmpty(address))
                            continue;

                        if (address.StartsWith("Assets/") || address.StartsWith("assets/"))
                            throw new Exception($"The address can not set asset path in collector : {CollectPath} \nAssetPath: {assetPath}");

                        if (addressTemper.TryGetValue(address, out var existed) == false)
                            addressTemper.Add(address, assetPath);
                        else
                            throw new Exception($"The address is existed : {address} in collector : {CollectPath} \nAssetPath:\n     {existed}\n     {assetPath}");
                    }
                }
            }

            // 返回列表
            return result.Values.ToList();
        }


        /// <summary>
        /// 创建资源收集类
        /// </summary>
        private CollectAssetInfo CreateCollectAssetInfo(CollectCommand command, AssetBundleCollectorGroup group, AssetInfo assetInfo)
        {
            string address = GetAddress(command, group, assetInfo);
            string bundleName = GetBundleName(command, group, assetInfo);
            List<string> assetTags = GetAssetTags(group);
            CollectAssetInfo collectAssetInfo = new CollectAssetInfo(CollectorType, bundleName, address, assetInfo, assetTags);
            collectAssetInfo.DependAssets = GetAllDependencies(command, assetInfo.AssetPath);
            return collectAssetInfo;
        }

        private bool IsCollectAsset(AssetBundleCollectorGroup group, AssetInfo assetInfo)
        {
            // 根据规则设置过滤资源文件
            IFilterRule filterRuleInstance = AssetBundleCollectorSettingData.GetFilterRuleInstance(FilterRuleName);
            return filterRuleInstance.IsCollectAsset(new FilterRuleData(assetInfo.AssetPath, CollectPath, group.GroupName, UserData));
        }
        private string GetAddress(CollectCommand command, AssetBundleCollectorGroup group, AssetInfo assetInfo)
        {
            if (command.EnableAddressable == false)
                return string.Empty;

            if (CollectorType != ECollectorType.MainAssetCollector)
                return string.Empty;

            IAddressRule addressRuleInstance = AssetBundleCollectorSettingData.GetAddressRuleInstance(AddressRuleName);
            string adressValue = addressRuleInstance.GetAssetAddress(new AddressRuleData(assetInfo.AssetPath, CollectPath, group.GroupName, UserData));
            return adressValue;
        }
        private string GetBundleName(CollectCommand command, AssetBundleCollectorGroup group, AssetInfo assetInfo)
        {
            if (command.AutoCollectShaders)
            {
                if (assetInfo.IsShaderAsset())
                {
                    // 获取着色器打包规则结果
                    PackRuleResult shaderPackRuleResult = DefaultPackRule.CreateShadersPackRuleResult();
                    return shaderPackRuleResult.GetBundleName(command.PackageName, command.UniqueBundleName);
                }
            }

            // 获取其它资源打包规则结果
            IPackRule packRuleInstance = AssetBundleCollectorSettingData.GetPackRuleInstance(PackRuleName);
            PackRuleResult defaultPackRuleResult = packRuleInstance.GetPackRuleResult(new PackRuleData(assetInfo.AssetPath, CollectPath, group.GroupName, UserData));
            return defaultPackRuleResult.GetBundleName(command.PackageName, command.UniqueBundleName);
        }
        private List<string> GetAssetTags(AssetBundleCollectorGroup group)
        {
            List<string> result = EditorTools.StringToStringList(AssetTags, ';');
            if (CollectorType == ECollectorType.MainAssetCollector)
            {
                List<string> temps = EditorTools.StringToStringList(group.AssetTags, ';');
                result.AddRange(temps);
            }
            return result;
        }
        private List<AssetInfo> GetAllDependencies(CollectCommand command, string mainAssetPath)
        {
            bool ignoreGetDependencies = command.IsFlagSet(ECollectFlags.IgnoreGetDependencies);
            if (ignoreGetDependencies)
                return new List<AssetInfo>();

            if (CollectorType == ECollectorType.MainAssetCollector &&
                string.Equals(
                    UserData,
                    StopDependencyTraversalAtCollectorRoot,
                    StringComparison.Ordinal) &&
                AssetDatabase.IsValidFolder(CollectPath))
            {
                return GetDependenciesOutsideCollectorRoot(
                    command,
                    mainAssetPath);
            }

            string[] depends = command.AssetDependency.GetDependencies(mainAssetPath, true);
            List<AssetInfo> result = new List<AssetInfo>(depends.Length);
            foreach (string assetPath in depends)
            {
                // 注意：排除主资源对象
                if (assetPath == mainAssetPath)
                    continue;

                AssetInfo assetInfo = new AssetInfo(assetPath);
                if (command.IgnoreRule.IsIgnore(assetInfo) == false)
                    result.Add(assetInfo);
            }
            return result;
        }

        private List<AssetInfo> GetDependenciesOutsideCollectorRoot(
            CollectCommand command,
            string mainAssetPath)
        {
            // Every asset below this collector root is already a main asset
            // with its own bundle. Stop traversing when such an asset is
            // reached instead of retaining the same large recursive
            // dependency graph once per scene. Reuse the traversal
            // collections across all assets in this collector to keep the
            // build's managed allocation bounded.
            _dependencyPathStack ??= new List<string>(32);
            _visitedDependencyPaths ??=
                new HashSet<string>(StringComparer.Ordinal);
            _sceneDependencyPaths ??= new List<string>(256);
            _sceneDependencyGuids ??=
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            _dependencyPathStack.Clear();
            _visitedDependencyPaths.Clear();
            _sceneDependencyPaths.Clear();
            _sceneDependencyGuids.Clear();

            string normalizedRoot =
                CollectPath.Replace('\\', '/').TrimEnd('/') + "/";
            var result = new List<AssetInfo>();
            _dependencyPathStack.Add(mainAssetPath);
            _visitedDependencyPaths.Add(mainAssetPath);
            try
            {
                while (_dependencyPathStack.Count > 0)
                {
                    int lastIndex = _dependencyPathStack.Count - 1;
                    string currentPath =
                        _dependencyPathStack[lastIndex];
                    _dependencyPathStack.RemoveAt(lastIndex);
                    IReadOnlyList<string> directDependencies =
                        currentPath.EndsWith(
                            ".unity",
                            StringComparison.OrdinalIgnoreCase)
                            ? ReadSceneDependenciesWithoutLoading(
                                currentPath)
                            : command.AssetDependency.GetDependencies(
                                currentPath,
                                false);
                    for (int dependencyIndex = 0;
                         dependencyIndex < directDependencies.Count;
                         dependencyIndex++)
                    {
                        string dependencyPath =
                            directDependencies[dependencyIndex];
                        if (!_visitedDependencyPaths.Add(
                                dependencyPath))
                        {
                            continue;
                        }

                        string normalizedDependency =
                            dependencyPath.Replace('\\', '/');
                        if (normalizedDependency.StartsWith(
                                normalizedRoot,
                                StringComparison.Ordinal))
                        {
                            // Assets below the collector root are already
                            // assigned to their own bundles. Keep the direct
                            // dependency so YooAsset can populate the asset-
                            // level DependBundleIDs used by runtime loading,
                            // but do not traverse through it again. Dropping
                            // the dependency here leaves serialized cross-
                            // bundle references (prefab -> texture, material
                            // -> shader, etc.) null in a player build.
                            var collectedDependency =
                                new AssetInfo(dependencyPath);
                            if (!command.IgnoreRule.IsIgnore(
                                    collectedDependency))
                            {
                                result.Add(collectedDependency);
                            }
                            continue;
                        }
                        if (!normalizedDependency.StartsWith(
                                "Assets/",
                                StringComparison.Ordinal) &&
                            !normalizedDependency.StartsWith(
                                "Packages/",
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var assetInfo = new AssetInfo(dependencyPath);
                        if (!command.IgnoreRule.IsIgnore(assetInfo))
                            result.Add(assetInfo);
                        _dependencyPathStack.Add(dependencyPath);
                    }
                }

                return result;
            }
            finally
            {
                _dependencyPathStack.Clear();
                _visitedDependencyPaths.Clear();
                _sceneDependencyPaths.Clear();
                _sceneDependencyGuids.Clear();
            }
        }

        private IReadOnlyList<string>
            ReadSceneDependenciesWithoutLoading(string scenePath)
        {
            _sceneDependencyPaths.Clear();
            _sceneDependencyGuids.Clear();
            const string guidMarker = "guid: ";
            foreach (string line in File.ReadLines(scenePath))
            {
                int searchStart = 0;
                while (searchStart < line.Length)
                {
                    int markerIndex = line.IndexOf(
                        guidMarker,
                        searchStart,
                        StringComparison.Ordinal);
                    if (markerIndex < 0)
                        break;

                    int guidStart =
                        markerIndex + guidMarker.Length;
                    const int guidLength = 32;
                    if (guidStart + guidLength > line.Length)
                        break;
                    string guid =
                        line.Substring(guidStart, guidLength);
                    searchStart = guidStart + guidLength;
                    if (!_sceneDependencyGuids.Add(guid))
                        continue;

                    string dependencyPath =
                        AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(dependencyPath))
                        _sceneDependencyPaths.Add(dependencyPath);
                }
            }

            return _sceneDependencyPaths;
        }
    }
}
