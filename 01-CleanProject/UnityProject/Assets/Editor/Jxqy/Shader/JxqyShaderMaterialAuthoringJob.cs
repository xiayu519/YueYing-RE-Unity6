using System;
using Jxqy.Editor.YooAsset;
using Jxqy.UnityAdapters;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Jxqy.Editor.Shader
{
    public static class JxqyShaderMaterialAuthoringJob
    {
        public const string ShaderRoot =
            "Assets/AssetRaw/Jxqy/Shaders";
        public const string MaterialRoot =
            "Assets/AssetRaw/Jxqy/Materials";

        [MenuItem("TEngine/Jxqy/Create Ported Shader Materials")]
        public static void Create()
        {
            EnsureAssetFolder(MaterialRoot);
            foreach (string key in JxqyMaterialCache.MaterialKeys)
                CreateOrUpdateMaterial(key);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateOrThrow();
            Debug.Log(
                $"Jxqy static shader materials are ready. " +
                $"Materials={JxqyMaterialCache.MaterialKeys.Count}; " +
                $"Root={MaterialRoot}.");
        }

        public static void ValidateOrThrow()
        {
            foreach (string key in JxqyMaterialCache.MaterialKeys)
            {
                string shaderPath = GetShaderPath(key);
                string materialPath = GetMaterialPath(key);
                UnityEngine.Shader shader =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(
                        shaderPath);
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (shader == null)
                {
                    throw new BuildFailedException(
                        $"Jxqy shader asset is missing: {shaderPath}");
                }
                if (material == null)
                {
                    throw new BuildFailedException(
                        $"Jxqy static material is missing: {materialPath}");
                }
                if (material.shader != shader)
                {
                    throw new BuildFailedException(
                        $"Jxqy material '{materialPath}' does not " +
                        $"reference '{shaderPath}'.");
                }

                string address =
                    JxqyAddressByRelativePath.CreateAddress(materialPath);
                string expectedAddress =
                    $"jxqy/materials/{key}.mat";
                if (!string.Equals(
                        address,
                        expectedAddress,
                        StringComparison.Ordinal))
                {
                    throw new BuildFailedException(
                        $"Jxqy material address mismatch: " +
                        $"expected '{expectedAddress}', got '{address}'.");
                }
            }
        }

        public static string GetMaterialPath(string key)
        {
            return $"{MaterialRoot}/{key}.mat";
        }

        public static string GetShaderPath(string key)
        {
            return $"{ShaderRoot}/{key}.shader";
        }

        private static void CreateOrUpdateMaterial(string key)
        {
            string shaderPath = GetShaderPath(key);
            UnityEngine.Shader shader =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(
                    shaderPath);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Jxqy shader asset is missing: {shaderPath}");
            }

            string materialPath = GetMaterialPath(key);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = key,
                };
                AssetDatabase.CreateAsset(material, materialPath);
                return;
            }

            if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }

    public sealed class JxqyShaderMaterialBuildValidator :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            JxqyShaderMaterialAuthoringJob.ValidateOrThrow();
        }
    }
}
