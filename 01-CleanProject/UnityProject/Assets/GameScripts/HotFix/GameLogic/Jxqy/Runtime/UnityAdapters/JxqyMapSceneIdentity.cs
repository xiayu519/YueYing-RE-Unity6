using System;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    /// <summary>
    /// Inspector-visible identity for a generated map scene. The key is a
    /// stable resource namespace and must not depend on the Unity scene path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JxqyMapSceneIdentity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Stable scene resource namespace. Keep this unchanged when moving the scene asset.")]
        private string _sceneKey = string.Empty;

        [SerializeField]
        [Tooltip("Stable ID of the source MAP used to generate this scene.")]
        private string _mapStableId = string.Empty;

        [SerializeField]
        [Tooltip("Reference-only path of the source MAP.")]
        private string _sourceRelativePath = string.Empty;

        public string SceneKey => _sceneKey;
        public string MapStableId => _mapStableId;
        public string SourceRelativePath => _sourceRelativePath;

        public void ConfigureGeneratedScene(
            string sceneKey,
            string mapStableId,
            string sourceRelativePath)
        {
            if (string.IsNullOrWhiteSpace(sceneKey))
                throw new ArgumentException(
                    "Scene key is empty.",
                    nameof(sceneKey));
            if (string.IsNullOrWhiteSpace(mapStableId))
                throw new ArgumentException(
                    "Map stable ID is empty.",
                    nameof(mapStableId));
            _sceneKey = sceneKey.Trim().Replace('\\', '/').ToLowerInvariant();
            _mapStableId =
                mapStableId.Trim().Replace('\\', '/').ToLowerInvariant();
            _sourceRelativePath =
                (sourceRelativePath ?? string.Empty).Replace('\\', '/');
        }
    }
}
