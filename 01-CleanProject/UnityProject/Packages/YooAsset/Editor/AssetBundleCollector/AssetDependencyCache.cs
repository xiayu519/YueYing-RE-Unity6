using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YooAsset.Editor
{
    public class AssetDependencyCache
    {
        private readonly AssetDependencyDatabase _database;
        private readonly bool _useAssetDependencyDatabase;

        /// <summary>
        /// 初始化资源依赖缓存系统
        /// </summary>
        public AssetDependencyCache(bool useAssetDependencyDB)
        {
            _useAssetDependencyDatabase = useAssetDependencyDB;
            if (!useAssetDependencyDB)
                return;

            Debug.Log("Use asset dependency database !");
            string databaseFilePath = "Library/AssetDependencyDB";
            _database = new AssetDependencyDatabase();
            _database.CreateDatabase(true, databaseFilePath);
            _database.SaveDatabase();
        }

        /// <summary>
        ///  获取资源的依赖列表
        /// </summary>
        /// <param name="assetPath">资源路径</param>
        /// <param name="recursive">递归查找所有依赖</param>
        /// <returns>返回依赖的资源路径集合</returns>
        public string[] GetDependencies(string assetPath, bool recursive = true)
        {
            // 通过本地缓存获取依赖关系
            if (_useAssetDependencyDatabase)
                return _database.GetDependencies(assetPath, recursive);
            return AssetDatabase.GetDependencies(assetPath, recursive);

            // 通过Unity引擎获取依赖关系
        }
    }
}
