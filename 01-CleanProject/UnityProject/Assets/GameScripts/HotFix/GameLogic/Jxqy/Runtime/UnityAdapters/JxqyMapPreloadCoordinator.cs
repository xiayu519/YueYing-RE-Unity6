using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Content;
using Jxqy.Ports;
using TEngine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Jxqy.UnityAdapters
{
    /// <summary>
    /// Keeps generated Jxqy scenes inside TEngine/YooAsset ownership. This
    /// adapter lives in Jxqy.Runtime, so it resolves the framework module
    /// directly instead of depending back on the GameLogic composition root.
    /// </summary>
    public sealed class JxqyTengineMapScenePort :
        IJxqyMapScenePort,
        IJxqyMapSceneIdentityPort
    {
        private readonly ISceneModule _scenes;
        private readonly Dictionary<string, Scene> _loadedScenes =
            new(StringComparer.OrdinalIgnoreCase);

        public JxqyTengineMapScenePort(ISceneModule scenes = null)
        {
            _scenes = scenes ?? ModuleSystem.GetModule<ISceneModule>();
            if (_scenes == null)
                throw new InvalidOperationException(
                    "TEngine scene module is unavailable.");
        }

        public async UniTask LoadAdditiveAsync(
            string address,
            Action<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateAddress(address);
            cancellationToken.ThrowIfCancellationRequested();
            Scene scene = await _scenes.LoadSceneAsync(
                address,
                LoadSceneMode.Additive,
                false,
                100,
                false,
                progress,
                JxqyResourceLocations.PackageName);
            if (cancellationToken.IsCancellationRequested)
            {
                if (scene.IsValid() && scene.isLoaded)
                    await _scenes.UnloadAsync(address);
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException(
                    $"TEngine loaded an invalid Jxqy scene: {address}");
            _loadedScenes[address] = scene;
        }

        public bool Activate(string address)
        {
            ValidateAddress(address);
            return _scenes.ActivateScene(address);
        }

        public async UniTask UnloadAsync(
            string address,
            CancellationToken cancellationToken = default)
        {
            ValidateAddress(address);
            cancellationToken.ThrowIfCancellationRequested();
            bool unloaded = await _scenes.UnloadAsync(address);
            if (!unloaded)
                throw new InvalidOperationException(
                    $"TEngine failed to unload Jxqy scene: {address}");
            _loadedScenes.Remove(address);
        }

        public bool TryReadSceneKey(
            string address,
            out string sceneKey)
        {
            ValidateAddress(address);
            sceneKey = string.Empty;
            if (!_loadedScenes.TryGetValue(address, out Scene scene) ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return false;
            }

            JxqyMapSceneIdentity identity = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                JxqyMapSceneIdentity candidate =
                    root.GetComponent<JxqyMapSceneIdentity>();
                if (candidate == null)
                    continue;
                if (identity != null)
                    return false;
                identity = candidate;
            }
            if (identity == null ||
                string.IsNullOrWhiteSpace(identity.SceneKey))
            {
                return false;
            }
            sceneKey = identity.SceneKey;
            return true;
        }

        private static void ValidateAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "Scene address is empty.",
                    nameof(address));
        }
    }

    public sealed class JxqyMapPreloadCoordinator : IDisposable
    {
        public const string PreloadManifestAddress =
            "jxqy/manifests/preload-manifest.json";
        public const int ResourceLoadBatchSize = 32;

        private readonly IJxqyResourcePort _resources;
        private readonly IJxqyMapScenePort _scenes;
        // The localized runtime renders legacy map data itself. Generated
        // Unity scenes only provide a common camera/rendering shell, so the
        // playable host can retain one shell just as the original game keeps
        // its game screen alive while replacing map data.
        private readonly bool _keepLoadedSceneAsRuntimeShell;
        private readonly List<IDisposable> _manifestLeases = new();
        private readonly Dictionary<string, JxqyResourceScope> _sharedScopes =
            new(StringComparer.OrdinalIgnoreCase);
        private JxqyPreloadManifest _manifest;
        private JxqyMapSceneCatalog _sceneCatalog;
        private JxqyResourceScope _activeMapScope;
        private string _activeMapStableId = string.Empty;
        private string _activeSceneAddress = string.Empty;
        private bool _disposed;

        public JxqyMapPreloadCoordinator(
            IJxqyResourcePort resources,
            IJxqyMapScenePort scenes = null,
            bool keepLoadedSceneAsRuntimeShell = false)
        {
            _resources = resources ??
                         throw new ArgumentNullException(nameof(resources));
            _scenes = scenes;
            _keepLoadedSceneAsRuntimeShell =
                keepLoadedSceneAsRuntimeShell;
        }

        public string ActiveMapStableId => _activeMapStableId;
        public string ActiveSceneAddress => _activeSceneAddress;
        public JxqyPreloadManifest Manifest => _manifest;

        public async UniTask LoadManifestAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_manifest != null)
                return;
            var scope = new JxqyResourceScope("preload-manifest");
            var candidateLeases = new List<IDisposable>(2);
            try
            {
                JxqyAssetLease<TextAsset> manifestLease =
                    await _resources.LoadAsync<TextAsset>(
                        PreloadManifestAddress,
                        scope,
                        cancellationToken);
                candidateLeases.Add(manifestLease);
                JxqyPreloadManifest parsed =
                    JsonUtility.FromJson<JxqyPreloadManifest>(
                        manifestLease.Asset.text);
                if (parsed == null || parsed.Errors == null ||
                    parsed.Errors.Count != 0)
                {
                    throw new InvalidOperationException(
                        "Preload manifest is missing or contains validation errors.");
                }

                JxqyMapSceneCatalog sceneCatalog = null;
                if (_scenes != null)
                {
                    JxqyAssetLease<TextAsset> catalogLease =
                        await _resources.LoadAsync<TextAsset>(
                            JxqyMapSceneCatalog.Address,
                            scope,
                            cancellationToken);
                    candidateLeases.Add(catalogLease);
                    sceneCatalog =
                        JsonUtility.FromJson<JxqyMapSceneCatalog>(
                            catalogLease.Asset.text);
                    if (sceneCatalog?.Maps == null ||
                        sceneCatalog.Maps.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "Map scene catalog is missing or empty.");
                    }
                }

                _manifest = parsed;
                _sceneCatalog = sceneCatalog;
                JxqyResourceAddressCatalog.Configure(
                    parsed,
                    string.Empty);
                _manifestLeases.AddRange(candidateLeases);
            }
            catch
            {
                foreach (IDisposable lease in candidateLeases)
                    lease.Dispose();
                await _resources.ReleaseScopeAsync(
                    scope,
                    CancellationToken.None);
                throw;
            }
        }

        public async UniTask SwitchMapAsync(
            string mapStableId,
            IProgress<JxqyPreloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_manifest == null)
                throw new InvalidOperationException(
                    "Preload manifest has not been loaded.");
            if (string.IsNullOrWhiteSpace(mapStableId))
                throw new ArgumentException(
                    "Map stable ID is empty.",
                    nameof(mapStableId));
            JxqyPreloadGroup group = _manifest.Groups.SingleOrDefault(
                candidate =>
                    candidate.Kind == "Map" &&
                    string.Equals(
                        candidate.OwnerStableId,
                        mapStableId,
                        StringComparison.OrdinalIgnoreCase));
            if (group == null)
                throw new KeyNotFoundException(
                    $"Map preload group not found: {mapStableId}");
            JxqyMapSceneEntry sceneEntry = null;
            if (_scenes != null)
            {
                sceneEntry = _sceneCatalog.Maps.Find(candidate =>
                    string.Equals(
                        candidate.MapStableId,
                        mapStableId,
                        StringComparison.OrdinalIgnoreCase));
                if (sceneEntry == null ||
                    string.IsNullOrWhiteSpace(sceneEntry.SceneAddress))
                {
                    throw new KeyNotFoundException(
                        $"Generated map scene not found: {mapStableId}");
                }
            }
            bool reuseActiveScene = sceneEntry != null &&
                                    !string.IsNullOrEmpty(
                                        _activeSceneAddress) &&
                                    (_keepLoadedSceneAsRuntimeShell ||
                                     string.Equals(
                                         _activeSceneAddress,
                                         sceneEntry.SceneAddress,
                                         StringComparison.OrdinalIgnoreCase));

            var candidateScope = new JxqyResourceScope(
                $"map:{Guid.NewGuid():N}:{mapStableId}");
            var candidateLeases = new List<IDisposable>(
                group.Resources.Count);
            bool candidateSceneLoaded = false;
            int totalSteps = group.Resources.Count +
                             (sceneEntry == null || reuseActiveScene
                                 ? 0
                                 : 1);
            try
            {
                await LoadResourceBatchesAsync(
                    group.Resources,
                    candidateScope,
                    candidateLeases,
                    mapStableId,
                    totalSteps,
                    progress,
                    cancellationToken);

                if (sceneEntry != null && !reuseActiveScene)
                {
                    string sceneAddress = sceneEntry.SceneAddress;
                    await _scenes.LoadAdditiveAsync(
                        sceneAddress,
                        sceneProgress =>
                        {
                            float normalized = totalSteps <= 0
                                ? 1
                                : (group.Resources.Count +
                                   Mathf.Clamp01(sceneProgress)) / totalSteps;
                            progress?.Report(new JxqyPreloadProgress(
                                mapStableId,
                                group.Resources.Count,
                                totalSteps,
                                sceneAddress,
                                "Scene",
                                normalized));
                        },
                        cancellationToken);
                    candidateSceneLoaded = true;
                    cancellationToken.ThrowIfCancellationRequested();
                    string expectedSceneKey = ResolveSceneKey(group);
                    if (_scenes is IJxqyMapSceneIdentityPort identityPort &&
                        (!identityPort.TryReadSceneKey(
                             sceneAddress,
                             out string actualSceneKey) ||
                         !string.Equals(
                             expectedSceneKey,
                             actualSceneKey,
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException(
                            $"Generated map scene identity mismatch. " +
                            $"Expected='{expectedSceneKey}', " +
                            $"Actual='{actualSceneKey ?? "<missing>"}', " +
                            $"Scene='{sceneAddress}'.");
                    }
                    if (!_scenes.Activate(sceneAddress))
                    {
                        throw new InvalidOperationException(
                            $"Failed to activate generated map scene: " +
                            $"{sceneAddress}");
                    }
                    progress?.Report(new JxqyPreloadProgress(
                        mapStableId,
                        totalSteps,
                        totalSteps,
                        sceneAddress,
                        "Scene"));
                }
            }
            catch (OperationCanceledException)
            {
                await ReleaseCandidateAsync(
                    candidateScope,
                    candidateLeases,
                    candidateSceneLoaded
                        ? sceneEntry?.SceneAddress
                        : null);
                throw;
            }
            catch (Exception exception)
            {
                await ReleaseCandidateAsync(
                    candidateScope,
                    candidateLeases,
                    candidateSceneLoaded
                        ? sceneEntry?.SceneAddress
                        : null);
                throw new InvalidOperationException(
                    $"Map switch preparation failed. Map='{mapStableId}', " +
                    $"Scene='{sceneEntry?.SceneAddress ?? "<resource-only>"}'.",
                    exception);
            }

            JxqyResourceScope previousScope = _activeMapScope;
            string previousSceneAddress = _activeSceneAddress;
            string committedSceneAddress = reuseActiveScene
                ? previousSceneAddress
                : sceneEntry?.SceneAddress ?? string.Empty;
            _activeMapScope = candidateScope;
            _activeMapStableId = mapStableId;
            _activeSceneAddress = committedSceneAddress;
            JxqyResourceAddressCatalog.SetActiveOwner(
                ResolveSceneKey(group));
            try
            {
                if (_scenes != null &&
                    !string.IsNullOrEmpty(previousSceneAddress) &&
                    !string.Equals(
                        previousSceneAddress,
                        _activeSceneAddress,
                        StringComparison.OrdinalIgnoreCase))
                {
                    await _scenes.UnloadAsync(
                        previousSceneAddress,
                        CancellationToken.None);
                }
                if (previousScope != null)
                {
                    await _resources.ReleaseScopeAsync(
                        previousScope,
                        CancellationToken.None);
                }
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Map switch committed but previous map cleanup failed. " +
                    $"ActiveMap='{mapStableId}', " +
                    $"PreviousScene='{previousSceneAddress}'.");
            }
        }

        private static string ResolveSceneKey(JxqyPreloadGroup group)
        {
            return string.IsNullOrWhiteSpace(group.SceneKey)
                ? group.OwnerStableId
                : group.SceneKey;
        }

        public async UniTask PreloadSharedAsync(
            string kind,
            IProgress<JxqyPreloadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_manifest == null)
                throw new InvalidOperationException(
                    "Preload manifest has not been loaded.");
            if (string.IsNullOrWhiteSpace(kind) ||
                string.Equals(kind, "Map", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Shared preload kind is invalid.",
                    nameof(kind));
            if (_sharedScopes.ContainsKey(kind))
                return;
            JxqyPreloadGroup group = _manifest.Groups.SingleOrDefault(
                candidate => string.Equals(
                    candidate.Kind,
                    kind,
                    StringComparison.OrdinalIgnoreCase));
            if (group == null)
                throw new KeyNotFoundException(
                    $"Shared preload group not found: {kind}");

            var scope = new JxqyResourceScope(
                $"shared:{kind}:{Guid.NewGuid():N}");
            var leases = new List<IDisposable>(group.Resources.Count);
            try
            {
                await LoadResourceBatchesAsync(
                    group.Resources,
                    scope,
                    leases,
                    group.OwnerStableId,
                    group.Resources.Count,
                    progress,
                    cancellationToken);
                _sharedScopes.Add(kind, scope);
            }
            catch
            {
                foreach (IDisposable lease in leases)
                    lease.Dispose();
                await _resources.ReleaseScopeAsync(
                    scope,
                    CancellationToken.None);
                throw;
            }
        }

        public async UniTask ReleaseSharedAsync(
            string kind,
            CancellationToken cancellationToken = default)
        {
            if (!_sharedScopes.TryGetValue(
                    kind,
                    out JxqyResourceScope scope))
                return;
            _sharedScopes.Remove(kind);
            await _resources.ReleaseScopeAsync(scope, cancellationToken);
        }

        public async UniTask ReleaseMapAsync(
            CancellationToken cancellationToken = default)
        {
            JxqyResourceScope scope = _activeMapScope;
            string sceneAddress = _activeSceneAddress;
            _activeMapScope = null;
            _activeMapStableId = string.Empty;
            _activeSceneAddress = string.Empty;
            if (_scenes != null && !string.IsNullOrEmpty(sceneAddress))
                await _scenes.UnloadAsync(
                    sceneAddress,
                    cancellationToken);
            if (scope != null)
                await _resources.ReleaseScopeAsync(
                    scope,
                    cancellationToken);
        }

        public void Dispose()
        {
            DisposeCore(releaseRuntimeResources: true);
        }

        /// <summary>
        /// Clears Jxqy ownership during process shutdown without scheduling
        /// work against TEngine modules that may already have shut down.
        /// </summary>
        public void DisposeForApplicationShutdown()
        {
            DisposeCore(releaseRuntimeResources: false);
        }

        private void DisposeCore(bool releaseRuntimeResources)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (IDisposable lease in _manifestLeases)
                lease.Dispose();
            _manifestLeases.Clear();
            if (releaseRuntimeResources && _activeMapScope != null)
                _resources.ReleaseScopeAsync(
                    _activeMapScope,
                    CancellationToken.None).Forget();
            if (releaseRuntimeResources &&
                _scenes != null &&
                !string.IsNullOrEmpty(_activeSceneAddress))
            {
                _scenes.UnloadAsync(
                    _activeSceneAddress,
                    CancellationToken.None).Forget();
            }
            _activeMapScope = null;
            _activeSceneAddress = string.Empty;
            if (releaseRuntimeResources)
            {
                foreach (JxqyResourceScope scope in _sharedScopes.Values)
                    _resources.ReleaseScopeAsync(
                        scope,
                        CancellationToken.None).Forget();
            }
            _sharedScopes.Clear();
        }

        private async UniTask ReleaseCandidateAsync(
            JxqyResourceScope scope,
            List<IDisposable> leases,
            string sceneAddress)
        {
            if (_scenes != null &&
                !string.IsNullOrEmpty(sceneAddress))
            {
                try
                {
                    await _scenes.UnloadAsync(
                        sceneAddress,
                        CancellationToken.None);
                }
                catch (Exception exception)
                {
                    TEngine.Log.Error(
                        $"Failed to roll back candidate scene " +
                        $"'{sceneAddress}': {exception}");
                }
            }
            foreach (IDisposable lease in leases)
                lease.Dispose();
            await _resources.ReleaseScopeAsync(
                scope,
                CancellationToken.None);
        }

        private async UniTask<IDisposable> LoadResourceAsync(
            JxqyPreloadResource resource,
            JxqyResourceScope scope,
            CancellationToken cancellationToken)
        {
            string address = resource.Address;
            if (address.EndsWith(
                    ".png",
                    StringComparison.OrdinalIgnoreCase) ||
                address.EndsWith(
                    ".jpg",
                    StringComparison.OrdinalIgnoreCase))
                return await _resources.LoadAsync<Texture2D>(
                    address,
                    scope,
                    cancellationToken);
            if (address.EndsWith(
                    ".wav",
                    StringComparison.OrdinalIgnoreCase))
                return await _resources.LoadAsync<AudioClip>(
                    address,
                    scope,
                    cancellationToken);
            if (address.EndsWith(
                    ".mp4",
                    StringComparison.OrdinalIgnoreCase))
                return await _resources.LoadAsync<VideoClip>(
                    address,
                    scope,
                    cancellationToken);
            return await _resources.LoadAsync<TextAsset>(
                address,
                scope,
                cancellationToken);
        }

        private async UniTask LoadResourceBatchesAsync(
            IReadOnlyList<JxqyPreloadResource> resources,
            JxqyResourceScope scope,
            List<IDisposable> leases,
            string ownerStableId,
            int progressTotal,
            IProgress<JxqyPreloadProgress> progress,
            CancellationToken cancellationToken)
        {
            for (int batchStart = 0;
                 batchStart < resources.Count;
                 batchStart += ResourceLoadBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int batchCount = Math.Min(
                    ResourceLoadBatchSize,
                    resources.Count - batchStart);
                var pending = new UniTask<IDisposable>[batchCount];
                for (int offset = 0; offset < batchCount; offset++)
                {
                    pending[offset] = LoadResourceAsync(
                        resources[batchStart + offset],
                        scope,
                        cancellationToken);
                }

                Exception firstError = null;
                for (int offset = 0; offset < batchCount; offset++)
                {
                    JxqyPreloadResource resource =
                        resources[batchStart + offset];
                    try
                    {
                        IDisposable lease = await pending[offset];
                        leases.Add(lease);
                        progress?.Report(new JxqyPreloadProgress(
                            ownerStableId,
                            batchStart + offset + 1,
                            progressTotal,
                            resource.Address,
                            "Resource"));
                    }
                    catch (Exception exception)
                    {
                        firstError ??= exception;
                    }
                }

                if (firstError != null)
                    throw firstError;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(JxqyMapPreloadCoordinator));
        }
    }

    public readonly struct JxqyPreloadProgress
    {
        public JxqyPreloadProgress(
            string ownerStableId,
            int completed,
            int total,
            string currentAddress,
            string phase = "Resource",
            float normalized = -1)
        {
            OwnerStableId = ownerStableId;
            Completed = completed;
            Total = total;
            CurrentAddress = currentAddress;
            Phase = phase;
            _normalized = normalized;
        }

        private readonly float _normalized;
        public string OwnerStableId { get; }
        public int Completed { get; }
        public int Total { get; }
        public string CurrentAddress { get; }
        public string Phase { get; }
        public float Normalized => _normalized >= 0
            ? Mathf.Clamp01(_normalized)
            : Total <= 0
                ? 1
                : (float)Completed / Total;
    }
}
