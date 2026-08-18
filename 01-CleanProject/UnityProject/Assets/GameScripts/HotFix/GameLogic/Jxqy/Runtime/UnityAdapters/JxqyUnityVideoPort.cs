using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Ports;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyUnityVideoPort : MonoBehaviour, IJxqyVideoPort
    {
        private IJxqyResourcePort _resources;
        private VideoPlayer _player;
        private JxqyResourceScope _scope;
        private IDisposable _lease;
        private UniTaskCompletionSource _playbackCompletion;
        private Canvas _overlayCanvas;
        private RawImage _overlayImage;
        private RenderTexture _targetTexture;
        private bool _ownsTargetTexture;
        private bool _isPaused;
        private bool _isPlaying;

        public event Action PlaybackStarted;
        public bool IsPlaying => _isPlaying;
        public bool IsPresentationActive =>
            _overlayCanvas != null &&
            _overlayCanvas.gameObject.activeInHierarchy;
#if UNITY_EDITOR
        public string LastRequestedAddress { get; private set; } =
            string.Empty;
#endif
        public bool IsOverlayTopmost =>
            _overlayCanvas != null &&
            _overlayCanvas.gameObject.activeInHierarchy &&
            _overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            _overlayCanvas.sortingOrder == short.MaxValue;

        public void Initialize(
            IJxqyResourcePort resources,
            RenderTexture targetTexture = null)
        {
            _resources = resources ??
                         throw new ArgumentNullException(nameof(resources));
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.audioOutputMode =
                VideoAudioOutputMode.Direct;
            _targetTexture = targetTexture ??
                new RenderTexture(
                    800,
                    600,
                    0,
                    RenderTextureFormat.ARGB32)
                {
                    name = "Jxqy Video Overlay Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
            _ownsTargetTexture = targetTexture == null;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _targetTexture;
            CreateOverlay();
        }

        public void BindCamera(Camera targetCamera)
        {
            EnsureInitialized();
            if (targetCamera == null)
                throw new ArgumentNullException(nameof(targetCamera));
        }

        public void RequestSkip()
        {
            _playbackCompletion?.TrySetResult();
        }

        public void ShowBlackTransition()
        {
            EnsureInitialized();
            ClearTargetTexture();
            SetOverlayVisible(true);
        }

        public async UniTask PlayAsync(
            string address,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
#if UNITY_EDITOR
            LastRequestedAddress = address ?? string.Empty;
#endif
            ReleasePlayback(hideOverlay: false);
            _scope = new JxqyResourceScope(
                $"video:{Guid.NewGuid():N}");
            JxqyAssetLease<VideoClip> lease =
                await _resources.LoadAsync<VideoClip>(
                    address,
                    _scope,
                    cancellationToken);
            _lease = lease;
            _player.clip = lease.Asset;
            var prepared =
                new UniTaskCompletionSource();
            var completed =
                new UniTaskCompletionSource();
            _playbackCompletion = completed;
            void OnPrepared(VideoPlayer _) => prepared.TrySetResult();
            void OnError(VideoPlayer _, string message) =>
                completed.TrySetException(
                    new InvalidOperationException(message));
            void OnCompleted(VideoPlayer _) =>
                completed.TrySetResult();
            _player.prepareCompleted += OnPrepared;
            _player.errorReceived += OnError;
            _player.loopPointReached += OnCompleted;
            try
            {
                ClearTargetTexture();
                SetOverlayVisible(true);
                _player.Prepare();
                await UniTask.WhenAny(
                    prepared.Task,
                    completed.Task);
                cancellationToken.ThrowIfCancellationRequested();
                if (completed.Task.Status ==
                    UniTaskStatus.Faulted)
                {
                    await completed.Task;
                }
                if (completed.Task.Status ==
                    UniTaskStatus.Succeeded)
                {
                    return;
                }
                _player.Play();
                _isPlaying = true;
                PlaybackStarted?.Invoke();
                if (_isPaused)
                    _player.Pause();
                await completed.Task.AttachExternalCancellation(
                    cancellationToken);
            }
            catch
            {
                Stop();
                throw;
            }
            finally
            {
                _isPlaying = false;
                _playbackCompletion = null;
                _player.prepareCompleted -= OnPrepared;
                _player.errorReceived -= OnError;
                _player.loopPointReached -= OnCompleted;
                Stop();
            }
        }

        public void Stop()
        {
            ReleasePlayback(hideOverlay: true);
        }

        private void ReleasePlayback(bool hideOverlay)
        {
            if (hideOverlay)
                SetOverlayVisible(false);
            if (_player != null)
            {
                _player.Stop();
                _player.clip = null;
            }
            _lease?.Dispose();
            _lease = null;
            if (_scope != null && _resources != null)
                _resources.ReleaseScopeAsync(
                    _scope,
                    CancellationToken.None).Forget();
            _scope = null;
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (_player == null || !_player.isPrepared)
                return;
            if (paused)
                _player.Pause();
            else
                _player.Play();
        }

        private void OnDestroy()
        {
            Stop();
            if (_ownsTargetTexture && _targetTexture != null)
            {
                _targetTexture.Release();
                if (Application.isPlaying)
                    Destroy(_targetTexture);
                else
                    DestroyImmediate(_targetTexture);
            }
            _targetTexture = null;
            _ownsTargetTexture = false;
        }

        private void CreateOverlay()
        {
            var overlay = new GameObject(
                "Jxqy Video Overlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            _overlayCanvas = overlay.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = short.MaxValue;
            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var imageObject = new GameObject(
                "Video",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(Button));
            imageObject.transform.SetParent(overlay.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _overlayImage = imageObject.GetComponent<RawImage>();
            _overlayImage.texture = _targetTexture;
            _overlayImage.color = Color.white;
            _overlayImage.raycastTarget = true;
            // UI Button 同时接收桌面鼠标和移动端触摸。
            Button skipButton = imageObject.GetComponent<Button>();
            skipButton.transition = Selectable.Transition.None;
            skipButton.onClick.AddListener(RequestSkip);
            overlay.SetActive(false);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_overlayCanvas != null)
                _overlayCanvas.gameObject.SetActive(visible);
        }

        private void ClearTargetTexture()
        {
            if (_targetTexture == null)
                return;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _targetTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private void EnsureInitialized()
        {
            if (_resources == null || _player == null)
                throw new InvalidOperationException(
                    "Jxqy video port has not been initialized.");
        }
    }
}
