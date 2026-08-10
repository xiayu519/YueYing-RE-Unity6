using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Ports;
using YooAsset;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyYooAssetResourcePort : IJxqyResourcePort, IDisposable
    {
        private readonly object _gate = new();
        private readonly Dictionary<JxqyResourceScope, HashSet<IDisposable>>
            _leases = new();
        private bool _disposed;

        public async UniTask<JxqyAssetLease<T>> LoadAsync<T>(
            string address,
            JxqyResourceScope scope,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "Asset address is empty.",
                    nameof(address));
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));
            ThrowIfDisposed();
            ResourcePackage package = GetPackage();
            AssetHandle handle = package.LoadAssetAsync<T>(address);
            try
            {
                await handle.Task.AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (handle.Status != EOperationStatus.Succeed)
                    throw new InvalidOperationException(
                        $"YooAsset failed to load '{address}': {handle.LastError}");
                T asset = handle.GetAssetObject<T>();
                if (asset == null)
                    throw new InvalidOperationException(
                        $"YooAsset loaded '{address}' without a {typeof(T).Name} asset.");

                ScopeLease scopeLease = null;
                JxqyAssetLease<T> result = null;
                scopeLease = new ScopeLease(() =>
                {
                    lock (_gate)
                    {
                        if (_leases.TryGetValue(
                                scope,
                                out HashSet<IDisposable> scopeLeases))
                        {
                            scopeLeases.Remove(scopeLease);
                            if (scopeLeases.Count == 0)
                                _leases.Remove(scope);
                        }
                    }
                    handle.Dispose();
                });
                result = new JxqyAssetLease<T>(
                    address,
                    asset,
                    scopeLease.Dispose);
                scopeLease.SetExternalLease(result);
                lock (_gate)
                {
                    ThrowIfDisposed();
                    if (!_leases.TryGetValue(
                            scope,
                            out HashSet<IDisposable> scopeLeases))
                    {
                        scopeLeases = new HashSet<IDisposable>();
                        _leases.Add(scope, scopeLeases);
                    }
                    scopeLeases.Add(scopeLease);
                }
                return result;
            }
            catch
            {
                if (handle.IsValid)
                    handle.Dispose();
                throw;
            }
        }

        public async UniTask ReleaseScopeAsync(
            JxqyResourceScope scope,
            CancellationToken cancellationToken = default)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));
            IDisposable[] snapshot;
            lock (_gate)
            {
                if (!_leases.TryGetValue(
                        scope,
                        out HashSet<IDisposable> scopeLeases))
                    return;
                snapshot = scopeLeases.ToArray();
                _leases.Remove(scope);
            }
            foreach (IDisposable lease in snapshot)
                lease.Dispose();
            ResourcePackage package = YooAssets.TryGetPackage(
                JxqyResourceLocations.PackageName);
            if (package != null)
            {
                AsyncOperationBase operation =
                    package.UnloadUnusedAssetsAsync();
                await operation.Task.AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
            }
        }

        public void Dispose()
        {
            IDisposable[] leases;
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                leases = _leases.Values
                    .SelectMany(value => value)
                    .ToArray();
                _leases.Clear();
            }
            foreach (IDisposable lease in leases)
                lease.Dispose();
        }

        private static ResourcePackage GetPackage()
        {
            ResourcePackage package = YooAssets.TryGetPackage(
                JxqyResourceLocations.PackageName);
            if (package == null)
                throw new InvalidOperationException(
                    $"YooAsset package '{JxqyResourceLocations.PackageName}' is not initialized.");
            return package;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(
                    nameof(JxqyYooAssetResourcePort));
        }

        private sealed class ScopeLease : IDisposable
        {
            private Action _release;
            private IDisposable _externalLease;

            public ScopeLease(Action release)
            {
                _release = release;
            }

            public void SetExternalLease(IDisposable lease)
            {
                _externalLease = lease;
            }

            public void Dispose()
            {
                Action release = Interlocked.Exchange(
                    ref _release,
                    null);
                release?.Invoke();
                _externalLease = null;
            }
        }
    }
}
