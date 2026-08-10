using System;
using System.Collections.Generic;
using System.Linq;
using Jxqy.Ports;
using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyUnityClock : IJxqyClock
    {
        public double UnscaledSeconds => Time.unscaledTimeAsDouble;
        public bool IsPaused { get; private set; }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }
    }

    public sealed class JxqyLifecycleCoordinator
    {
        private readonly IJxqyClock _clock;
        private readonly IReadOnlyList<IJxqyInputPort> _inputs;
        private readonly IJxqyAudioPort _audio;
        private readonly IJxqyVideoPort _video;
        private bool _hasFocus = true;
        private bool _applicationPaused;
        private bool _effectivePaused;

        public JxqyLifecycleCoordinator(
            IJxqyClock clock,
            IEnumerable<IJxqyInputPort> inputs,
            IJxqyAudioPort audio = null,
            IJxqyVideoPort video = null)
        {
            _clock = clock ??
                     throw new ArgumentNullException(nameof(clock));
            _inputs = (inputs ??
                       throw new ArgumentNullException(nameof(inputs)))
                .Where(value => value != null)
                .Distinct()
                .ToArray();
            _audio = audio;
            _video = video;
        }

        public bool IsPaused => _effectivePaused;

        public void SetFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            Apply();
        }

        public void SetApplicationPaused(bool paused)
        {
            _applicationPaused = paused;
            Apply();
        }

        private void Apply()
        {
            bool shouldPause = !_hasFocus || _applicationPaused;
            if (shouldPause == _effectivePaused)
                return;

            // Reset before both transitions. On resume this activates the
            // desktop neutral gate and clears every stale mobile touch.
            foreach (IJxqyInputPort input in _inputs)
                input.ResetTransientState();
            _clock.SetPaused(shouldPause);
            _audio?.SetPaused(shouldPause);
            _video?.SetPaused(shouldPause);
            _effectivePaused = shouldPause;
        }
    }

    public sealed class JxqyApplicationLifecycle : MonoBehaviour
    {
        private JxqyLifecycleCoordinator _coordinator;
        private bool _hasFocus = true;
        private bool _applicationPaused;

        public bool IsPaused =>
            _coordinator != null && _coordinator.IsPaused;

        public void Initialize(
            IJxqyClock clock,
            IEnumerable<IJxqyInputPort> inputs,
            IJxqyAudioPort audio = null,
            IJxqyVideoPort video = null)
        {
            _coordinator = new JxqyLifecycleCoordinator(
                clock,
                inputs,
                audio,
                video);
            _coordinator.SetFocus(_hasFocus);
            _coordinator.SetApplicationPaused(_applicationPaused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            _coordinator?.SetFocus(hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            _coordinator?.SetApplicationPaused(paused);
        }
    }
}
