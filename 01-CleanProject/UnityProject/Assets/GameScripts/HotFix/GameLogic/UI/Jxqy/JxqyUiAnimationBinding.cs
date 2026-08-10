using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Animation;
using Jxqy.Domain.Content;
using Jxqy.UnityAdapters;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// Reusable TEngine/YooAsset-backed ASF animation binding for UGUI
    /// RawImages. It preserves the original global ASF size and loops using the
    /// converted per-frame durations.
    /// </summary>
    internal sealed class JxqyUiAnimationBinding : IDisposable
    {
        private readonly RawImage _target;
        private readonly List<Texture2D> _atlases = new();
        private CancellationTokenSource _cancellation;
        private IResourceModule _resources;
        private TextAsset _metadataAsset;
        private JxqyAnimationPlayer _player;
        private string _requestedKey = string.Empty;
        private int _version;
        private bool _preserveNativeSize;

        public JxqyUiAnimationBinding(RawImage target)
        {
            _target = target;
        }

        public bool IsReady => _player != null;

        public void Set(
            string category,
            string fileName,
            bool preserveNativeSize = true,
            int direction = 0)
        {
            if (_target == null ||
                string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(fileName))
            {
                _requestedKey = string.Empty;
                ResetLoadedAssets(clearTarget: true);
                return;
            }

            string safeFileName = Path.GetFileName(
                fileName.Replace('\\', '/'));
            string normalizedCategory =
                category.Trim().ToLowerInvariant();
            if (!JxqyResourceAddressCatalog.TryResolveAnimationAddress(
                    safeFileName,
                    out string metadataAddress,
                    normalizedCategory))
            {
                _requestedKey = string.Empty;
                ResetLoadedAssets(clearTarget: true);
                JxqyResourceAddressCatalog.ReportMissing(
                    $"animated UI {normalizedCategory}",
                    safeFileName);
                return;
            }

            string requestedKey =
                $"{normalizedCategory}/{safeFileName}/{direction}"
                    .ToLowerInvariant();
            if (string.Equals(
                    _requestedKey,
                    requestedKey,
                    StringComparison.Ordinal) &&
                (_cancellation != null || _player != null))
            {
                return;
            }

            ResetLoadedAssets(clearTarget: false);
            _requestedKey = requestedKey;
            _preserveNativeSize = preserveNativeSize;
            _cancellation = new CancellationTokenSource();
            int version = ++_version;
            LoadAsync(
                    metadataAddress,
                    direction,
                    version,
                    _cancellation.Token)
                .Forget();
        }

        public void Tick(float elapsedSeconds)
        {
            if (_player == null || elapsedSeconds < 0f)
                return;
            _player.Advance(elapsedSeconds);
            ApplyCurrentFrame();
        }

        public void Dispose()
        {
            _version++;
            _requestedKey = string.Empty;
            ResetLoadedAssets(clearTarget: false);
        }

        private async UniTaskVoid LoadAsync(
            string metadataAddress,
            int direction,
            int version,
            CancellationToken cancellationToken)
        {
            IResourceModule resources = GameModule.Resource;
            TextAsset metadataAsset = null;
            var atlases = new List<Texture2D>();
            try
            {
                if (resources == null)
                {
                    throw new InvalidOperationException(
                        "TEngine resource module is unavailable.");
                }

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
                if (metadata?.Frames == null ||
                    metadata.Frames.Count == 0 ||
                    metadata.AtlasAddresses == null ||
                    metadata.AtlasAddresses.Count == 0)
                {
                    throw new InvalidDataException(
                        $"Original UI animation metadata is invalid: " +
                        $"{metadataAddress}");
                }

                foreach (string atlasAddress in metadata.AtlasAddresses)
                {
                    Texture2D atlas =
                        await resources.LoadAssetAsync<Texture2D>(
                            atlasAddress,
                            cancellationToken,
                            JxqyResourceLocations.PackageName);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (atlas == null)
                    {
                        throw new InvalidDataException(
                            $"Original UI atlas is missing: {atlasAddress}");
                    }
                    atlases.Add(atlas);
                }
                if (version != _version || _target == null)
                    return;

                _resources = resources;
                _metadataAsset = metadataAsset;
                metadataAsset = null;
                _atlases.AddRange(atlases);
                atlases.Clear();
                _player = new JxqyAnimationPlayer(metadata);
                _player.SetDirection(direction);
                if (_preserveNativeSize)
                {
                    _target.rectTransform.sizeDelta = new Vector2(
                        metadata.GlobalWidth,
                        metadata.GlobalHeight);
                }
                ApplyCurrentFrame();
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
                    $"Jxqy original UI animation failed to load: " +
                    $"{metadataAddress}. {exception.Message}");
            }
            finally
            {
                if (metadataAsset != null)
                    resources?.UnloadAsset(metadataAsset);
                foreach (Texture2D atlas in atlases)
                    resources?.UnloadAsset(atlas);
            }
        }

        private void ApplyCurrentFrame()
        {
            if (_target == null || _player == null)
                return;
            JxqyAnimationFrameMetadata frame = _player.CurrentFrame;
            if (frame.AtlasPage < 0 ||
                frame.AtlasPage >= _atlases.Count)
            {
                return;
            }
            Texture2D atlas = _atlases[frame.AtlasPage];
            _target.texture = atlas;
            _target.uvRect = new Rect(
                (float)frame.AtlasX / atlas.width,
                (float)frame.AtlasY / atlas.height,
                (float)frame.AtlasWidth / atlas.width,
                (float)frame.AtlasHeight / atlas.height);
        }

        private void ResetLoadedAssets(bool clearTarget)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _player = null;
            if (clearTarget && _target != null)
            {
                _target.texture = null;
                _target.uvRect = new Rect(0f, 0f, 1f, 1f);
                _target.color = Color.clear;
            }
            if (_metadataAsset != null)
                _resources?.UnloadAsset(_metadataAsset);
            foreach (Texture2D atlas in _atlases)
                _resources?.UnloadAsset(atlas);
            _metadataAsset = null;
            _atlases.Clear();
            _resources = null;
        }
    }
}
