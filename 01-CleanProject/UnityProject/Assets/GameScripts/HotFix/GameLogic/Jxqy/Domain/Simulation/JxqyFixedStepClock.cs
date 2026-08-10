using System;

namespace Jxqy.Domain.Simulation
{
    public sealed class JxqyFixedStepClock
    {
        public const double DefaultStepSeconds = 1.0 / 60.0;
        public const int DefaultMaximumStepsPerFrame = 8;

        private double _accumulatorSeconds;

        public JxqyFixedStepClock(
            double stepSeconds = DefaultStepSeconds,
            int maximumStepsPerFrame = DefaultMaximumStepsPerFrame)
        {
            if (stepSeconds <= 0 || double.IsNaN(stepSeconds) ||
                double.IsInfinity(stepSeconds))
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            if (maximumStepsPerFrame <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumStepsPerFrame));
            StepSeconds = stepSeconds;
            MaximumStepsPerFrame = maximumStepsPerFrame;
        }

        public double StepSeconds { get; }
        public int MaximumStepsPerFrame { get; }
        public double SimulationSeconds { get; private set; }
        public long StepCount { get; private set; }
        public bool IsPaused { get; private set; }
        public double InterpolationAlpha =>
            _accumulatorSeconds / StepSeconds;

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        public int Advance(double elapsedSeconds, Action<double> fixedUpdate)
        {
            if (fixedUpdate == null)
                throw new ArgumentNullException(nameof(fixedUpdate));
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (IsPaused)
                return 0;

            double maximumAccepted =
                StepSeconds * MaximumStepsPerFrame;
            _accumulatorSeconds += Math.Min(
                elapsedSeconds,
                maximumAccepted);
            int executed = 0;
            while (_accumulatorSeconds + 1e-12 >= StepSeconds &&
                   executed < MaximumStepsPerFrame)
            {
                fixedUpdate(StepSeconds);
                _accumulatorSeconds -= StepSeconds;
                SimulationSeconds += StepSeconds;
                StepCount++;
                executed++;
            }
            if (_accumulatorSeconds >= StepSeconds)
                _accumulatorSeconds %= StepSeconds;
            return executed;
        }

        public void Reset()
        {
            _accumulatorSeconds = 0;
            SimulationSeconds = 0;
            StepCount = 0;
            IsPaused = false;
        }
    }
}
