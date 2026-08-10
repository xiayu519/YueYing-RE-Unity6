using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.UnityAdapters;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    internal sealed class JxqyUiTextureBinding : IDisposable
    {
        private readonly RawImage _target;
        private CancellationTokenSource _cancellation;
        private IResourceModule _resources;
        private Texture2D _texture;
        private string _address = string.Empty;
        private int _version;

        public JxqyUiTextureBinding(RawImage target)
        {
            _target = target;
            ClearTarget();
        }

        public Texture2D Texture => _texture;

        public void Set(string address)
        {
            string normalized = (address ?? string.Empty)
                .Trim()
                .Replace('\\', '/')
                .ToLowerInvariant();
            if (string.Equals(_address, normalized,
                    StringComparison.Ordinal) &&
                (_cancellation != null || _texture != null))
            {
                return;
            }

            ResetLoadedAsset();
            _address = normalized;
            if (string.IsNullOrWhiteSpace(normalized))
                return;
            _cancellation = new CancellationTokenSource();
            int version = ++_version;
            LoadAsync(normalized, version, _cancellation.Token).Forget();
        }

        public void Dispose()
        {
            _version++;
            _address = string.Empty;
            ResetLoadedAsset();
        }

        private async UniTaskVoid LoadAsync(
            string address,
            int version,
            CancellationToken cancellationToken)
        {
            IResourceModule resources = GameModule.Resource;
            Texture2D texture = null;
            try
            {
                if (resources == null)
                    throw new InvalidOperationException(
                        "TEngine resource module is unavailable.");
                texture = await resources.LoadAssetAsync<Texture2D>(
                    address,
                    cancellationToken,
                    JxqyResourceLocations.PackageName);
                cancellationToken.ThrowIfCancellationRequested();
                if (texture == null)
                    throw new InvalidOperationException(
                        $"UI texture is missing: {address}");
                if (version != _version || _target == null)
                    return;

                _resources = resources;
                _texture = texture;
                texture = null;
                _target.texture = _texture;
                _target.uvRect = new Rect(0f, 0f, 1f, 1f);
                _target.color = Color.white;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (version == _version)
                    _address = string.Empty;
                Log.Warning(
                    $"Jxqy UI texture failed to load: {address}. " +
                    exception.Message);
            }
            finally
            {
                if (texture != null)
                    resources?.UnloadAsset(texture);
            }
        }

        private void ResetLoadedAsset()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            ClearTarget();
            if (_texture != null)
                _resources?.UnloadAsset(_texture);
            _texture = null;
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
