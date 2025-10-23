using System;
using System.Diagnostics;
using System.Threading;

namespace UniqueLootHelper.Managers
{
    /// <summary>
    ///     Manages FPS limiting to reduce CPU usage and stabilize frame rendering
    /// </summary>
    public class FpsLimiter
    {
        private readonly Stopwatch _fpsCounter;
        private readonly Stopwatch _frameTimer;
        private int _frameCount;
        private DateTime _lastFpsUpdate;
        private TimeSpan _targetFrameTime;

        /// <summary>
        ///     Initializes a new instance of FpsLimiter
        /// </summary>
        /// <param name="targetFps">Target FPS (0 = unlimited)</param>
        public FpsLimiter(int targetFps = 60)
        {
            _frameTimer = new Stopwatch();
            _fpsCounter = new Stopwatch();
            _lastFpsUpdate = DateTime.Now;
            SetTargetFps(targetFps);
        }

        /// <summary>
        ///     Gets the current FPS (updated every second)
        /// </summary>
        public double CurrentFps { get; private set; }

        /// <summary>
        ///     Gets the target FPS limit
        /// </summary>
        public int TargetFps { get; private set; }

        /// <summary>
        ///     Sets the target FPS limit
        /// </summary>
        /// <param name="targetFps">Target FPS (0 = unlimited)</param>
        public void SetTargetFps(int targetFps)
        {
            TargetFps = targetFps;

            if (targetFps > 0)
            {
                _targetFrameTime = TimeSpan.FromMilliseconds(1000.0 / targetFps);
            }
            else
            {
                _targetFrameTime = TimeSpan.Zero;
            }
        }

        /// <summary>
        ///     Call this at the beginning of each frame
        /// </summary>
        public void StartFrame()
        {
            _frameTimer.Restart();

            if (!_fpsCounter.IsRunning)
            {
                _fpsCounter.Start();
            }
        }

        /// <summary>
        ///     Call this at the end of each frame to limit FPS
        /// </summary>
        /// <param name="enabled">Whether FPS limiting is enabled</param>
        public void EndFrame(bool enabled)
        {
            _frameCount++;

            // Update FPS counter every second
            TimeSpan timeSinceLastUpdate = DateTime.Now - _lastFpsUpdate;
            if (timeSinceLastUpdate.TotalSeconds >= 1.0)
            {
                CurrentFps = _frameCount / timeSinceLastUpdate.TotalSeconds;
                _frameCount = 0;
                _lastFpsUpdate = DateTime.Now;
            }

            // Apply FPS limiting if enabled and target is set
            if (!enabled || TargetFps <= 0)
            {
                return;
            }

            TimeSpan elapsed = _frameTimer.Elapsed;
            TimeSpan sleepTime = _targetFrameTime - elapsed;

            if (sleepTime > TimeSpan.Zero)
            {
                // Sleep for the remaining time to limit FPS
                // Using SpinWait for more accurate timing
                if (sleepTime.TotalMilliseconds > 1)
                {
                    Thread.Sleep((int)sleepTime.TotalMilliseconds - 1);
                }

                // Spin for remaining sub-millisecond time for accuracy
                while (_frameTimer.Elapsed < _targetFrameTime)
                {
                    Thread.SpinWait(10);
                }
            }
        }

        /// <summary>
        ///     Gets frame time statistics
        /// </summary>
        /// <returns>Tuple of (current frame time in ms, target frame time in ms)</returns>
        public (double currentMs, double targetMs) GetFrameTimeStats()
        {
            return (_frameTimer.Elapsed.TotalMilliseconds, _targetFrameTime.TotalMilliseconds);
        }

        /// <summary>
        ///     Resets the FPS counter
        /// </summary>
        public void Reset()
        {
            _frameCount = 0;
            CurrentFps = 0;
            _lastFpsUpdate = DateTime.Now;
            _fpsCounter.Restart();
        }
    }
}
