using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Content;
using Jxqy.UnityAdapters;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// Loads one original ASF frame through the TEngine/YooAsset lifecycle and
    /// applies the converted atlas UV to a UGUI RawImage.
    /// </summary>
    internal sealed class JxqyUiFrameBinding : IDisposable
    {
        private readonly RawImage _target;
        private readonly bool _preserveTargetSize;
        private CancellationTokenSource _cancellation;
        private IResourceModule _resources;
        private TextAsset _metadataAsset;
        private Texture2D _atlas;
        private string _requestedKey = string.Empty;
        private int _version;

        public JxqyUiFrameBinding(
            RawImage target,
            bool preserveTargetSize = false)
        {
            _target = target;
            _preserveTargetSize = preserveTargetSize;
            ClearTarget();
        }

        public void Set(string category, string fileName)
        {
            if (_target == null ||
                string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(fileName))
            {
                _requestedKey = string.Empty;
                ResetLoadedAssets();
                return;
            }

            string safeFileName = Path.GetFileName(
                fileName.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                _requestedKey = string.Empty;
                ResetLoadedAssets();
                return;
            }

            string normalizedCategory =
                category.Trim().ToLowerInvariant();
            if (!JxqyResourceAddressCatalog.TryResolveAnimationAddress(
                    safeFileName,
                    out string metadataAddress,
                    normalizedCategory))
            {
                _requestedKey = string.Empty;
                ResetLoadedAssets();
                JxqyResourceAddressCatalog.ReportMissing(
                    $"UI {normalizedCategory}",
                    safeFileName);
                return;
            }
            string requestedKey =
                $"{normalizedCategory}/{safeFileName}"
                    .ToLowerInvariant();
            if (string.Equals(
                    _requestedKey,
                    requestedKey,
                    StringComparison.Ordinal) &&
                (_cancellation != null || _atlas != null))
            {
                return;
            }

            ResetLoadedAssets();
            _requestedKey = requestedKey;
            _cancellation = new CancellationTokenSource();
            int version = ++_version;
            LoadAsync(
                    metadataAddress,
                    version,
                    _cancellation.Token)
                .Forget();
        }

        public void Dispose()
        {
            _version++;
            _requestedKey = string.Empty;
            ResetLoadedAssets();
        }

        private async UniTaskVoid LoadAsync(
            string metadataAddress,
            int version,
            CancellationToken cancellationToken)
        {
            IResourceModule resources = GameModule.Resource;
            TextAsset metadataAsset = null;
            Texture2D atlas = null;
            try
            {
                if (resources == null)
                    throw new InvalidOperationException(
                        "TEngine resource module is unavailable.");

                metadataAsset =
                    await resources.LoadAssetAsync<TextAsset>(
                        metadataAddress,
                        cancellationToken,
                        JxqyResourceLocations.PackageName);
                cancellationToken.ThrowIfCancellationRequested();
                JxqyAnimationMetadata metadata = metadataAsset == null
                    ? null
                    : JsonUtility.FromJson<JxqyAnimationMetadata>(
                        metadataAsset.text);
                JxqyAnimationFrameMetadata frame =
                    metadata?.Frames != null &&
                    metadata.Frames.Count > 0
                        ? metadata.Frames[0]
                        : null;
                if (frame == null ||
                    metadata.AtlasAddresses == null ||
                    frame.AtlasPage < 0 ||
                    frame.AtlasPage >= metadata.AtlasAddresses.Count)
                {
                    throw new InvalidDataException(
                        $"Original UI frame metadata is invalid: " +
                        $"{metadataAddress}");
                }

                atlas = await resources.LoadAssetAsync<Texture2D>(
                    metadata.AtlasAddresses[frame.AtlasPage],
                    cancellationToken,
                    JxqyResourceLocations.PackageName);
                cancellationToken.ThrowIfCancellationRequested();
                if (atlas == null)
                    throw new InvalidDataException(
                        $"Original UI atlas is missing: " +
                        $"{metadata.AtlasAddresses[frame.AtlasPage]}");
                if (version != _version || _target == null)
                    return;

                _resources = resources;
                _metadataAsset = metadataAsset;
                _atlas = atlas;
                metadataAsset = null;
                atlas = null;
                _target.texture = _atlas;
                _target.uvRect = new Rect(
                    (float)frame.AtlasX / _atlas.width,
                    (float)frame.AtlasY / _atlas.height,
                    (float)frame.AtlasWidth / _atlas.width,
                    (float)frame.AtlasHeight / _atlas.height);
                RectTransform targetRect = _target.rectTransform;
                if (targetRect != null && !_preserveTargetSize)
                {
                    targetRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        frame.AtlasWidth);
                    targetRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        frame.AtlasHeight);
                }
                _target.color = Color.white;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (version == _version)
                    _requestedKey = string.Empty;
                Log.Warning(
                    $"Jxqy original UI frame failed to load: " +
                    $"{metadataAddress}. {exception.Message}");
            }
            finally
            {
                if (metadataAsset != null)
                    resources?.UnloadAsset(metadataAsset);
                if (atlas != null)
                    resources?.UnloadAsset(atlas);
            }
        }

        private void ResetLoadedAssets()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            ClearTarget();
            if (_metadataAsset != null)
                _resources?.UnloadAsset(_metadataAsset);
            if (_atlas != null)
                _resources?.UnloadAsset(_atlas);
            _metadataAsset = null;
            _atlas = null;
            _resources = null;
        }

        private void ClearTarget()
        {
            if (_target == null)
                return;
            _target.texture = null;
            _target.uvRect = new Rect(0f, 0f, 1f, 1f);
            _target.color = Color.clear;
        }
    }
}
