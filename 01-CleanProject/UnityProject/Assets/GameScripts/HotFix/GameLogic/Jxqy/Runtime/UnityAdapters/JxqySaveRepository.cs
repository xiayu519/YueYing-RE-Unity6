using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Persistence;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqySaveRepository
    {
        private static readonly UTF8Encoding Utf8NoBom =
            new(false, true);
        private readonly IJxqyPersistencePort _persistence;

        public JxqySaveRepository(IJxqyPersistencePort persistence)
        {
            _persistence = persistence ??
                           throw new ArgumentNullException(
                               nameof(persistence));
        }

        public async UniTask SaveAsync(
            int slot,
            JxqySaveGameData save,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            if (save == null)
                throw new ArgumentNullException(nameof(save));
            save.SchemaVersion =
                JxqySaveGameData.CurrentSchemaVersion;
            save.SavedUtc = DateTime.UtcNow.ToString("O");
            Normalize(save);
            ValidateStructure(save, slot);
            save.ContentHash = string.Empty;
            save.ContentHash = ComputeContentHash(save);
            byte[] bytes = Utf8NoBom.GetBytes(
                JsonUtility.ToJson(save, true));
            await _persistence.WriteAtomicAsync(
                GetSlotPath(slot),
                bytes,
                cancellationToken);
        }

        public async UniTask<JxqySaveGameData> LoadAsync(
            int slot,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            byte[] bytes = await _persistence.ReadAsync(
                GetSlotPath(slot),
                cancellationToken);
            JxqySaveGameData save;
            try
            {
                save = JsonUtility.FromJson<JxqySaveGameData>(
                    Utf8NoBom.GetString(bytes));
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is DecoderFallbackException)
            {
                throw new InvalidDataException(
                    $"Save slot {slot} contains invalid JSON.",
                    exception);
            }
            if (save == null)
                throw new InvalidDataException(
                    $"Save slot {slot} is invalid JSON.");
            if (save.SchemaVersion <
                    JxqySaveGameData.OldestSupportedSchemaVersion ||
                save.SchemaVersion >
                    JxqySaveGameData.CurrentSchemaVersion)
            {
                throw new NotSupportedException(
                    $"Save schema {save.SchemaVersion} is not supported.");
            }
            if (save.SchemaVersion ==
                JxqySaveGameData.CurrentSchemaVersion)
            {
                ValidateContentHash(save, slot);
            }
            Normalize(save);
            ValidateStructure(save, slot);
            if (save.SchemaVersion <
                JxqySaveGameData.CurrentSchemaVersion)
            {
                save.SchemaVersion =
                    JxqySaveGameData.CurrentSchemaVersion;
                save.ContentHash = string.Empty;
            }
            return save;
        }

        public async UniTask<JxqySaveGameData> LoadOrDeleteInvalidAsync(
            int slot,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await LoadAsync(slot, cancellationToken);
            }
            catch (Exception exception)
                when (exception is InvalidDataException ||
                      exception is NotSupportedException)
            {
                await DeleteSlotAsync(slot, cancellationToken);
                return null;
            }
        }

        public async UniTask<JxqySaveGameData> ImportLegacyGameIniAsync(
            byte[] legacyBytes,
            CancellationToken cancellationToken = default)
        {
            if (legacyBytes == null)
                throw new ArgumentNullException(nameof(legacyBytes));
            cancellationToken.ThrowIfCancellationRequested();
            string text = DecodeLegacy(legacyBytes);
            await UniTask.Yield(
                cancellationToken: cancellationToken);
            return JxqyLegacySaveImporter.ImportGameIni(text);
        }

        public bool Exists(int slot)
        {
            ValidateSlot(slot);
            return _persistence.Exists(GetSlotPath(slot));
        }

        public bool SnapshotExists(int slot)
        {
            ValidateSlot(slot);
            return _persistence.Exists(GetSnapshotPath(slot));
        }

        public async UniTask DeleteAllAsync(
            CancellationToken cancellationToken = default)
        {
            for (int slot = 0; slot <= 7; slot++)
            {
                await _persistence.DeleteAsync(
                    GetSlotPath(slot),
                    cancellationToken);
                await _persistence.DeleteAsync(
                    GetSnapshotPath(slot),
                    cancellationToken);
            }
        }

        public async UniTask DeleteSlotAsync(
            int slot,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            await _persistence.DeleteAsync(
                GetSlotPath(slot),
                cancellationToken);
            await _persistence.DeleteAsync(
                GetSnapshotPath(slot),
                cancellationToken);
        }

        public UniTask SaveSnapshotAsync(
            int slot,
            byte[] pngBytes,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            if (pngBytes == null || pngBytes.Length == 0)
                throw new ArgumentException(
                    "Snapshot PNG is empty.",
                    nameof(pngBytes));
            return _persistence.WriteAtomicAsync(
                GetSnapshotPath(slot),
                pngBytes,
                cancellationToken);
        }

        public UniTask<byte[]> LoadSnapshotAsync(
            int slot,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            return _persistence.ReadAsync(
                GetSnapshotPath(slot),
                cancellationToken);
        }

        private static string DecodeLegacy(byte[] bytes)
        {
            try
            {
                return Utf8NoBom.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(936).GetString(bytes);
            }
        }

        private static void Normalize(JxqySaveGameData save)
        {
            save.World ??= new JxqySaveWorldState();
            save.Player ??= new JxqySavePlayerState();
            save.Presentation ??= new JxqySavePresentationState();
            save.Variables ??= new();
            save.ParallelScripts ??= new();
            save.Memos ??= new();
            save.LegacyFiles ??= new();
            save.World.Npcs ??= new();
            save.World.Objects ??= new();
            save.World.Traps ??= new();
            save.World.NpcSnapshots ??= new();
            save.World.ObjectSnapshots ??= new();
            save.Player.Profiles ??= new();
            foreach (JxqySaveNpcSnapshot snapshot in
                     save.World.NpcSnapshots)
            {
                if (snapshot != null)
                    snapshot.Npcs ??= new();
            }
            foreach (JxqySaveObjectSnapshot snapshot in
                     save.World.ObjectSnapshots)
            {
                if (snapshot != null)
                    snapshot.Objects ??= new();
            }
        }

        private static void ValidateStructure(
            JxqySaveGameData save,
            int slot)
        {
            if (save.World == null ||
                save.Player == null ||
                save.Presentation == null ||
                save.Variables == null ||
                save.ParallelScripts == null ||
                save.Memos == null ||
                save.LegacyFiles == null)
            {
                throw new InvalidDataException(
                    $"Save slot {slot} is missing required state.");
            }
            if (string.IsNullOrWhiteSpace(save.World.Map))
            {
                throw new InvalidDataException(
                    $"Save slot {slot} has no map identifier.");
            }
        }

        private static void ValidateContentHash(
            JxqySaveGameData save,
            int slot)
        {
            string expected = save.ContentHash;
            if (string.IsNullOrWhiteSpace(expected))
            {
                throw new InvalidDataException(
                    $"Save slot {slot} has no integrity hash.");
            }
            save.ContentHash = string.Empty;
            string actual = ComputeContentHash(save);
            save.ContentHash = expected;
            if (!string.Equals(
                    expected,
                    actual,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Save slot {slot} failed its integrity check.");
            }
        }

        private static string ComputeContentHash(JxqySaveGameData save)
        {
            byte[] payload = Utf8NoBom.GetBytes(
                JsonUtility.ToJson(save, false));
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(payload);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private static string GetSlotPath(int slot)
        {
            return $"Saves/Slot{slot}/save-v1.json";
        }

        private static string GetSnapshotPath(int slot)
        {
            return $"Saves/Slot{slot}/snapshot.png";
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot > 7)
                throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
