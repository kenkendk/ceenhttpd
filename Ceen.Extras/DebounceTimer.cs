using System.Net.Http.Headers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Extras
{
    /// <summary>
    /// Shared interface for the debounce timer
    /// </summary>
    public interface IDebounceTimer
    {
        /// <summary>
        /// Signals the debounce timer to run the action
        /// </summary>
        /// <param name="condition">A flag used to choose if the notification should be triggered</param>
        /// <returns><c>true</c> if the signal was used, <c>false</c> if the signal was ignored</returns>
        bool Notify(bool condition = true);

    }

    /// <summary>
    /// Helper class to allow cancellation of a pending task
    /// </summary>
    public class CancelAbleDelayedTask : IDisposable
    {
        /// <summary>
        /// The cancelation token used to control cancellation of the task
        /// </summary>
        private readonly CancellationTokenSource m_tcs = new CancellationTokenSource();
        /// <summary>
        /// The pending task
        /// </summary>
        private readonly Task m_task;

        /// <summary>
        /// Make the cancelable delayed task awaitable
        /// </summary>
        System.Runtime.CompilerServices.TaskAwaiter GetAwaiter() => m_task.GetAwaiter();

        /// <summary>
        /// Constructs a new cancel-able delayed task
        /// </summary>
        /// <param name="action">The method to execute after the time has passed</param>
        /// <param name="delay">The delay to use before running the task</param>
        public CancelAbleDelayedTask(Action action, TimeSpan delay)
        {
            m_task = 
                Task.Delay(delay, m_tcs.Token)
                .ContinueWith(_ => action(), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>
        /// Disposes all held resources
        /// </summary>
        public void Dispose()
        {
            m_tcs.Cancel();
        }
    }

    /// <summary>
    /// Helper class for debouncing events.
    /// Note that this class does not guarantee that
    /// invocations cannot run in parallel.
    /// Note that repeated notification of the timer
    /// can cause the action to never execute,
    /// as it is repeatedly postponed
    /// </summary>
    public class DebounceTimerPostpone : IDebounceTimer
    {
        /// <summary>
        /// The internal runner task
        /// </summary>
        private CancelAbleDelayedTask m_runner;

        /// <summary>
        /// The action to run
        /// </summary>
        private readonly Action m_action;
        /// <summary>
        /// The delay between each run
        /// </summary>
        private readonly TimeSpan m_delay;

        /// <summary>
        /// Constructs a new debounce timer
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="delay">The delay between each run</param>
        /// <param name="notify">Notify on construction</param>
        public DebounceTimerPostpone(Action action, TimeSpan delay, bool notify = false)
        {
            if (delay < TimeSpan.FromMilliseconds(1))
                throw new ArgumentOutOfRangeException("The delay must be at least one millisecond");

            m_delay = delay;
            m_action = action ?? throw new ArgumentNullException(nameof(action));

            if (notify)
                Notify();
        }

        /// <summary>
        /// Triggers the timer and runs the action after the timer has expired
        /// </summary>
        /// <param name="condition">A flag used to choose if the notification should be triggered</param>
        /// <returns><c>true</c> if the signal was used, <c>false</c> if the signal was ignored</returns>
        public bool Notify(bool condition = true)
        {
            if (!condition)
                return false;

            var task = new CancelAbleDelayedTask(m_action, m_delay);

            var prev = System.Threading.Interlocked.Exchange(ref m_runner, task);
            if (prev != task)
                prev?.Dispose(); // Stop the previous task            

            return true;
        }
    }

    /// <summary>
    /// Helper class for debouncing events.
    /// This class runs at most once for each delay.
    /// This class guarantees that only one task can
    /// run at a time, and will restart the timer
    /// after running, if signalled while running.
    /// </summary>
    public class DebounceTimerRepeat : IDebounceTimer
    {
        /// <summary>
        /// The lock guarding the runner
        /// </summary>
        private readonly object m_lock = new object();
        /// <summary>
        /// The action to run
        /// </summary>
        private readonly Action m_action;
        /// <summary>
        /// The delay between each run
        /// </summary>
        private readonly TimeSpan m_delay;
        /// <summary>
        /// The current runner, if any
        /// </summary>
        private Task m_runner;

        /// <summary>
        /// Flag used to check if we should re-activate
        /// </summary>
        private int m_reactivate;

        /// <summary>
        /// Constructs a new debounce timer
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="delay">The delay between each run</param>
        /// <param name="notify">Notify on construction</param>
        public DebounceTimerRepeat(Action action, TimeSpan delay, bool notify = false)
        {
            if (delay < TimeSpan.FromMilliseconds(1))
                throw new ArgumentOutOfRangeException("The delay must be at least one millisecond");
            m_delay = delay;
            m_action = action ?? throw new ArgumentNullException(nameof(action));

            if (notify)
                Notify();
        }

        /// <summary>
        /// Triggers the timer and runs the action after the timer has expired
        /// </summary>
        /// <param name="condition">A flag used to choose if the notification should be triggered</param>
        /// <returns><c>true</c> if the signal was used, <c>false</c> if the signal was ignored</returns>
        public bool Notify(bool condition = true)
        {
            if (!condition)
                return false;

            // Register that we want to run
            System.Threading.Interlocked.Increment(ref m_reactivate);

            // As we read/write the m_runner variable, acquire the lock
            lock(m_lock)
            {
                // Check if we are not running,
                // the IsCompleted check guards against a crashed task
                if (m_runner == null || m_runner.IsCompleted)
                {
                    m_runner = Task.Delay(m_delay).ContinueWith(_ => {
                        // Note that the lock is NOT taken when
                        // this method runs

                        // Clear any requests prior to running the action
                        System.Threading.Interlocked.Exchange(ref m_reactivate, 0);

                        try
                        {
                            m_action();
                        }
                        finally
                        {
                            // Check if any new requests are recorded while running
                            if (System.Threading.Interlocked.Exchange(ref m_reactivate, 0) != 0)
                            {
                                // Prematurely signal that we have completed
                                lock(m_lock)
                                    m_runner = null;

                                // Then call notify, when we know that the runner is null            
                                Notify(true);
                            }
                        }
                    });

                    return true;
                }
            }

            return false;            
        }
    }

    /// <summary>
    /// A debounce timer that runs on first notification,
    /// and then ignores subsequent notifications until a
    /// certain period
    /// </summary>
    public class DebounceTimerRatelimit : IDebounceTimer
    {
        /// <summary>
        /// The time when a new runner is accepted
        /// </summary>
        private DateTime m_ignoreUntil;

        /// <summary>
        /// The lock guarding the runner
        /// </summary>
        private readonly object m_lock = new object();
        /// <summary>
        /// The action to run
        /// </summary>
        private readonly Action m_action;
        /// <summary>
        /// The delay between each run
        /// </summary>
        private readonly TimeSpan m_delay;

        /// <summary>
        /// Constructs a new debounce timer
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="delay">The delay between each run</param>
        /// <param name="notify">Notify on construction</param>
        public DebounceTimerRatelimit(Action action, TimeSpan delay, bool notify = false)
        {
            if (delay < TimeSpan.FromMilliseconds(1))
                throw new ArgumentOutOfRangeException("The delay must be at least one millisecond");
            m_delay = delay;
            m_action = action ?? throw new ArgumentNullException(nameof(action));
            m_ignoreUntil = DateTime.Now.AddSeconds(-1);
            
            if (notify)
                Notify();            
        }

        /// <summary>
        /// Triggers the timer and runs the action after the timer has expired
        /// </summary>
        /// <param name="condition">A flag used to choose if the notification should be triggered</param>
        /// <returns><c>true</c> if the signal was used, <c>false</c> if the signal was ignored</returns>
        public bool Notify(bool condition = true)
        {
            if (!condition)
                return false;

            if (DateTime.Now > m_ignoreUntil)
            {
                // Variable to allow running the action without holding the lock
                var runnotify = false;
                lock(m_lock)
                {
                    if (DateTime.Now > m_ignoreUntil)
                    {
                        m_ignoreUntil = DateTime.Now + m_delay;
                        runnotify = true;
                    }
                }

                if (runnotify)
                {
                    m_action();
                    return true;
                }
            }

            return false;
        }
    }
}