using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyFilePersistencePort : IJxqyPersistencePort
    {
        private readonly string _root;

        public string RootPath => _root;

        public JxqyFilePersistencePort(string root = null)
        {
            _root = Path.GetFullPath(
                root ?? Path.Combine(
                    Application.persistentDataPath,
                    "Jxqy"));
        }

        public bool Exists(string relativePath)
        {
            return File.Exists(Resolve(relativePath));
        }

        public async UniTask<byte[]> ReadAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            string path = Resolve(relativePath);
            return await UniTask.RunOnThreadPool(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return File.ReadAllBytes(path);
                },
                cancellationToken: cancellationToken);
        }

        public async UniTask WriteAtomicAsync(
            string relativePath,
            byte[] bytes,
            CancellationToken cancellationToken = default)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            string path = Resolve(relativePath);
            await UniTask.RunOnThreadPool(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string directory = Path.GetDirectoryName(path);
                    Directory.CreateDirectory(directory);
                    string temporary = path + ".tmp";
                    string backup = path + ".bak";
                    try
                    {
                        using (var stream = new FileStream(
                                   temporary,
                                   FileMode.Create,
                                   FileAccess.Write,
                                   FileShare.None,
                                   4096,
                                   FileOptions.WriteThrough))
                        {
                            stream.Write(bytes, 0, bytes.Length);
                            stream.Flush(flushToDisk: true);
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (File.Exists(path))
                        {
                            File.Replace(temporary, path, backup);
                        }
                        else
                        {
                            File.Move(temporary, path);
                        }
                    }
                    catch
                    {
                        if (File.Exists(temporary))
                            File.Delete(temporary);
                        throw;
                    }
                },
                cancellationToken: cancellationToken);
        }

        public async UniTask DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            string path = Resolve(relativePath);
            await UniTask.RunOnThreadPool(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.Exists(path))
                        File.Delete(path);
                },
                cancellationToken: cancellationToken);
        }

        private string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException(
                    "Persistence path is empty.",
                    nameof(relativePath));
            string normalized = relativePath.Replace(
                '\\',
                Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
                throw new ArgumentException(
                    "Persistence path must be relative.",
                    nameof(relativePath));
            string full = Path.GetFullPath(Path.Combine(_root, normalized));
            if (!full.StartsWith(
                    _root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Persistence path escapes its root.",
                    nameof(relativePath));
            return full;
        }
    }
}
