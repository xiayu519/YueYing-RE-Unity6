using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jxqy.Ports;
using TEngine;
using UnityEngine;
using FrameworkAudioType = TEngine.AudioType;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyUnityAudioPort :
        MonoBehaviour,
        IJxqyAudioPort,
        IJxqyWorldAudioPort
    {
        private const float LegacySoundMaxDistance = 1000f;
        private readonly HashSet<AudioAgent> _soundAgents = new();
        private readonly Dictionary<string, WorldSound> _worldSounds =
            new(StringComparer.OrdinalIgnoreCase);
        private IAudioModule _audioModule;
        private AudioAgent _musicAgent;
        private AudioAgent _ambientAgent;
        private float _musicVolume = 1f;
        private float _soundVolume = 1f;
        private bool _isPaused;
        private Jxqy.Domain.World.JxqyFloat2 _worldListener;

        public int RegisteredWorldSoundCount => _worldSounds.Count;

        public void Initialize(IAudioModule audioModule)
        {
            _audioModule = audioModule ??
                           throw new ArgumentNullException(
                               nameof(audioModule));
            _audioModule.Enable = true;
            _audioModule.Volume = 1f;
            _audioModule.SoundEnable = true;
            _audioModule.UISoundEnable = true;
            _audioModule.MusicVolume = _musicVolume;
            SetSoundVolume(1f);
        }

        public UniTask PlayMusicAsync(
            string address,
            bool loop,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAddress(address);
            StopMusic();
            _musicAgent = _audioModule.Play(
                FrameworkAudioType.Music,
                address,
                loop,
                _musicVolume,
                bAsync: true,
                bInPool: true,
                packageName: JxqyResourceLocations.PackageName);
            if (_isPaused)
                _musicAgent?.Pause();
            return UniTask.CompletedTask;
        }

        public void StopMusic()
        {
            _musicAgent?.Stop();
            _musicAgent = null;
        }

        public void StopSounds()
        {
            foreach (AudioAgent agent in _soundAgents)
                agent?.Stop();
            _soundAgents.Clear();
        }

        public UniTask PlaySoundAsync(
            string address,
            float volume,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAddress(address);
            _soundAgents.RemoveWhere(agent =>
                agent == null || agent.IsFree);
            // World loops retain Sound agents so their pan/volume can be
            // updated as the listener moves. If a foreground script sound
            // reuses one of those channels, the retained world owner can
            // overwrite its volume on the next update and make effects such
            // as OpenBox appear to fail intermittently. Use the independent
            // non-positional pool for foreground one-shots.
            AudioAgent soundAgent = _audioModule.Play(
                FrameworkAudioType.UISound,
                address,
                volume: Mathf.Clamp01(volume),
                bAsync: true,
                bInPool: true,
                packageName: JxqyResourceLocations.PackageName);
            if (soundAgent != null)
            {
                _soundAgents.Add(soundAgent);
                if (_isPaused)
                    soundAgent.Pause();
            }
            return UniTask.CompletedTask;
        }

        public UniTask PlayAmbientLoopAsync(
            string address,
            float volume,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAddress(address);
            StopAmbientLoop();
            _ambientAgent = _audioModule.Play(
                FrameworkAudioType.Sound,
                address,
                bLoop: true,
                volume: Mathf.Clamp01(volume),
                bAsync: true,
                bInPool: true,
                packageName: JxqyResourceLocations.PackageName);
            if (_isPaused)
                _ambientAgent?.Pause();
            return UniTask.CompletedTask;
        }

        public void StopAmbientLoop()
        {
            _ambientAgent?.Stop();
            _ambientAgent = null;
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            if (paused)
            {
                _musicAgent?.Pause();
                _ambientAgent?.Pause();
                foreach (AudioAgent agent in _soundAgents)
                    agent?.Pause();
            }
            else
            {
                _musicAgent?.UnPause();
                _ambientAgent?.UnPause();
                foreach (AudioAgent agent in _soundAgents)
                    agent?.UnPause();
            }
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            if (_audioModule != null)
                _audioModule.MusicVolume = _musicVolume;
            if (_musicAgent != null)
                _musicAgent.Volume = _musicVolume;
        }

        public void SetSoundVolume(float volume)
        {
            _soundVolume = Mathf.Clamp01(volume);
            if (_audioModule == null)
                return;
            _audioModule.SoundVolume = _soundVolume;
            _audioModule.UISoundVolume = _soundVolume;
        }

        public UniTask RegisterWorldSoundAsync(
            string id,
            string address,
            bool loop,
            Jxqy.Domain.World.JxqyFloat2 worldPosition,
            float volume,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAddress(address);
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "World sound id must not be empty.",
                    nameof(id));
            RemoveWorldSound(id);
            var sound = new WorldSound
            {
                Address = address,
                Position = worldPosition,
                Volume = Mathf.Clamp01(volume),
                Loop = loop,
                NextRandomPlayTime = Time.unscaledTime +
                                     UnityEngine.Random.Range(0.5f, 4f),
            };
            _worldSounds.Add(id, sound);
            if (loop)
                sound.Agent = CreateWorldAgent(sound, true);
            ApplyWorldSoundMix(sound);
            return UniTask.CompletedTask;
        }

        public UniTask PlayWorldSoundOnceAsync(
            string address,
            Jxqy.Domain.World.JxqyFloat2 worldPosition,
            float volume,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAddress(address);
            _soundAgents.RemoveWhere(agent =>
                agent == null || agent.IsFree);
            var sound = new WorldSound
            {
                Address = address,
                Position = worldPosition,
                Volume = Mathf.Clamp01(volume),
            };
            AudioAgent agent = CreateWorldAgent(sound, false);
            sound.Agent = agent;
            if (agent != null)
            {
                _soundAgents.Add(agent);
                ApplyWorldSoundMix(sound);
            }
            return UniTask.CompletedTask;
        }

        public void SetWorldSoundPosition(
            string id,
            Jxqy.Domain.World.JxqyFloat2 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !_worldSounds.TryGetValue(id, out WorldSound sound))
            {
                return;
            }
            sound.Position = worldPosition;
            ApplyWorldSoundMix(sound);
        }

        public void RemoveWorldSound(string id)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !_worldSounds.TryGetValue(id, out WorldSound sound))
            {
                return;
            }
            sound.Agent?.Stop();
            _worldSounds.Remove(id);
        }

        public void ClearWorldSounds()
        {
            foreach (WorldSound sound in _worldSounds.Values)
                sound.Agent?.Stop();
            _worldSounds.Clear();
        }

        public void SetWorldSoundListener(
            Jxqy.Domain.World.JxqyFloat2 worldPosition)
        {
            _worldListener = worldPosition;
            foreach (WorldSound sound in _worldSounds.Values)
                ApplyWorldSoundMix(sound);
        }

        private void Update()
        {
            if (_isPaused || _audioModule == null)
                return;
            float now = Time.unscaledTime;
            foreach (WorldSound sound in _worldSounds.Values)
            {
                if (sound.Loop || now < sound.NextRandomPlayTime)
                    continue;
                sound.Agent = CreateWorldAgent(sound, false);
                sound.NextRandomPlayTime =
                    now + UnityEngine.Random.Range(0.5f, 6f);
                ApplyWorldSoundMix(sound);
            }
        }

        private void OnDestroy()
        {
            StopMusic();
            StopAmbientLoop();
            ClearWorldSounds();
            StopSounds();
        }

        private void EnsureInitialized()
        {
            if (_audioModule == null)
                throw new InvalidOperationException(
                    "Jxqy audio port has not been initialized.");
        }

        private static void EnsureAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "Audio address must not be empty.",
                    nameof(address));
        }

        private AudioAgent CreateWorldAgent(WorldSound sound, bool loop)
        {
            AudioAgent agent = _audioModule.Play(
                FrameworkAudioType.Sound,
                sound.Address,
                bLoop: loop,
                volume: 0f,
                bAsync: true,
                bInPool: true,
                packageName: JxqyResourceLocations.PackageName);
            if (_isPaused)
                agent?.Pause();
            return agent;
        }

        private void ApplyWorldSoundMix(WorldSound sound)
        {
            if (sound.Agent == null)
                return;
            float deltaX = sound.Position.X - _worldListener.X;
            float deltaY = sound.Position.Y - _worldListener.Y;
            float distance = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY);
            float attenuation = Mathf.Clamp01(
                1f - distance / LegacySoundMaxDistance);
            sound.Agent.Volume = sound.Volume * attenuation;
            AudioSource source = sound.Agent.AudioResource();
            if (source == null)
                return;
            source.spatialBlend = 0f;
            source.panStereo = Mathf.Clamp(
                deltaX / LegacySoundMaxDistance,
                -1f,
                1f);
        }

        private sealed class WorldSound
        {
            public string Address;
            public Jxqy.Domain.World.JxqyFloat2 Position;
            public float Volume;
            public bool Loop;
            public float NextRandomPlayTime;
            public AudioAgent Agent;
        }
    }
}
