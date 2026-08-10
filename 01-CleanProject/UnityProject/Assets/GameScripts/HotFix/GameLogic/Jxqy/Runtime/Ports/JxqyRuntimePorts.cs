using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Domain.Input;
using Jxqy.Domain.World;
using UnityEngine;

namespace Jxqy.Ports
{
    public interface IJxqyClock
    {
        double UnscaledSeconds { get; }
        bool IsPaused { get; }
        void SetPaused(bool paused);
    }

    public interface IJxqyInputPort
    {
        JxqyInputFrame CaptureFrame();
        IReadOnlyList<JxqyInputIntent> CaptureIntents();
        void ResetTransientState();
    }

    public interface IJxqyRandomPort
    {
        int Next(int minimumInclusive, int maximumExclusive);
        float NextSingle();
    }

    public interface IJxqyResourcePort
    {
        UniTask<JxqyAssetLease<T>> LoadAsync<T>(
            string address,
            JxqyResourceScope scope,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask ReleaseScopeAsync(
            JxqyResourceScope scope,
            CancellationToken cancellationToken = default);
    }

    public interface IJxqyMapScenePort
    {
        UniTask LoadAdditiveAsync(
            string address,
            Action<float> progress = null,
            CancellationToken cancellationToken = default);

        bool Activate(string address);

        UniTask UnloadAsync(
            string address,
            CancellationToken cancellationToken = default);
    }

    public interface IJxqyMapSceneIdentityPort
    {
        bool TryReadSceneKey(
            string address,
            out string sceneKey);
    }

    public interface IJxqyRenderPort
    {
        void Submit(IReadOnlyList<JxqyDrawCommand> commands);
        void SetLogicalResolution(int width, int height);
    }

    public interface IJxqyAudioPort
    {
        UniTask PlayMusicAsync(
            string address,
            bool loop,
            CancellationToken cancellationToken = default);
        void StopMusic();
        void StopSounds();
        UniTask PlaySoundAsync(
            string address,
            float volume,
            CancellationToken cancellationToken = default);
        UniTask PlayAmbientLoopAsync(
            string address,
            float volume,
            CancellationToken cancellationToken = default);
        void StopAmbientLoop();
        void SetMusicVolume(float volume);
        void SetSoundVolume(float volume);
        void SetPaused(bool paused);
    }

    /// <summary>
    /// Optional positional ambience extension used by legacy map objects.
    /// Kept separate from IJxqyAudioPort so non-Unity test ports do not need
    /// to emulate Unity audio emitters.
    /// </summary>
    public interface IJxqyWorldAudioPort
    {
        UniTask RegisterWorldSoundAsync(
            string id,
            string address,
            bool loop,
            JxqyFloat2 worldPosition,
            float volume,
            CancellationToken cancellationToken = default);
        UniTask PlayWorldSoundOnceAsync(
            string address,
            JxqyFloat2 worldPosition,
            float volume,
            CancellationToken cancellationToken = default);
        void SetWorldSoundPosition(string id, JxqyFloat2 worldPosition);
        void RemoveWorldSound(string id);
        void ClearWorldSounds();
        void SetWorldSoundListener(JxqyFloat2 worldPosition);
    }

    public interface IJxqyVideoPort
    {
        UniTask PlayAsync(
            string address,
            CancellationToken cancellationToken = default);
        void Stop();
        void SetPaused(bool paused);
    }

    public interface IJxqyPersistencePort
    {
        UniTask<byte[]> ReadAsync(
            string relativePath,
            CancellationToken cancellationToken = default);
        UniTask WriteAtomicAsync(
            string relativePath,
            byte[] bytes,
            CancellationToken cancellationToken = default);
        UniTask DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default);
        bool Exists(string relativePath);
    }

    public readonly struct JxqyInputFrame
    {
        public JxqyInputFrame(
            long sequence,
            float moveX,
            float moveY,
            float pointerX,
            float pointerY,
            JxqyInputButtons buttons)
        {
            Sequence = sequence;
            MoveX = moveX;
            MoveY = moveY;
            PointerX = pointerX;
            PointerY = pointerY;
            Buttons = buttons;
        }

        public long Sequence { get; }
        public float MoveX { get; }
        public float MoveY { get; }
        public float PointerX { get; }
        public float PointerY { get; }
        public JxqyInputButtons Buttons { get; }
    }

    [Flags]
    public enum JxqyInputButtons
    {
        None = 0,
        Interact = 1 << 0,
        Attack = 1 << 1,
        Skill1 = 1 << 2,
        Skill2 = 1 << 3,
        Skill3 = 1 << 4,
        UseItem = 1 << 5,
        Menu = 1 << 6,
        Confirm = 1 << 7,
        Cancel = 1 << 8,
        PointerPrimary = 1 << 9,
        RunModifier = 1 << 10,
        JumpModifier = 1 << 11,
        LegacyKeyboardMovement = 1 << 12
    }

    public readonly struct JxqyDrawCommand
    {
        public JxqyDrawCommand(
            string textureAddress,
            Rect source,
            Vector2 position,
            Vector2 anchor,
            Color color,
            int depth,
            string materialKey)
        {
            TextureAddress = textureAddress;
            Source = source;
            Position = position;
            Anchor = anchor;
            Color = color;
            Depth = depth;
            MaterialKey = materialKey;
        }

        public string TextureAddress { get; }
        public Rect Source { get; }
        public Vector2 Position { get; }
        public Vector2 Anchor { get; }
        public Color Color { get; }
        public int Depth { get; }
        public string MaterialKey { get; }
    }

    public sealed class JxqyResourceScope : IEquatable<JxqyResourceScope>
    {
        public JxqyResourceScope(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Resource scope ID is empty.",
                    nameof(id));
            Id = id;
        }

        public string Id { get; }

        public bool Equals(JxqyResourceScope other)
        {
            return other != null &&
                   string.Equals(
                       Id,
                       other.Id,
                       StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as JxqyResourceScope);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Id);
        }
    }

    public sealed class JxqyAssetLease<T> : IDisposable
        where T : UnityEngine.Object
    {
        private Action _release;

        public JxqyAssetLease(
            string address,
            T asset,
            Action release)
        {
            Address = address;
            Asset = asset;
            _release = release ??
                       throw new ArgumentNullException(nameof(release));
        }

        public string Address { get; }
        public T Asset { get; }
        public bool IsReleased => _release == null;

        public void Dispose()
        {
            Action release = Interlocked.Exchange(
                ref _release,
                null);
            release?.Invoke();
        }
    }
}
