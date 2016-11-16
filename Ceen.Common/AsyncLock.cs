using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace Ceen
{
	// Implementation based on: http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx

	/// <summary>
	/// Implementation of a Semaphore that is usable with await statements
	/// </summary>
	public class AsyncSemaphore
	{
		/// <summary>
		/// A task signaling completion
		/// </summary>
		private readonly static Task s_completed = Task.FromResult(true);
		/// <summary>
		/// The list of waiters
		/// </summary>
		private readonly Queue<TaskCompletionSource<bool>> m_waiters = new Queue<TaskCompletionSource<bool>>();
		/// <summary>
		/// The additional number of release calls
		/// </summary>
		private int m_currentCount;

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.AsyncSemaphore"/> class.
		/// </summary>
		/// <param name="initialCount">The number of callers to allow before blocking.</param>
		public AsyncSemaphore(int initialCount)
		{
			if (initialCount < 0)
				throw new ArgumentOutOfRangeException(nameof(initialCount));

			m_currentCount = initialCount;
		}

		/// <summary>
		/// Waits for the semaphire to be released
		/// </summary>
		/// <returns>The awaitable task.</returns>
		public Task WaitAsync()
		{
			lock (m_waiters)
			{
				if (m_currentCount > 0)
				{
					--m_currentCount;
					return s_completed;
				}
				else
				{
					var waiter = new TaskCompletionSource<bool>();
					m_waiters.Enqueue(waiter);
					return waiter.Task;
				}
			}
		}

		/// <summary>
		/// Releases the semaphore to a new 
		/// </summary>
		public void Release()
		{
			TaskCompletionSource<bool> result = null;
			lock (m_waiters)
			{
				if (m_waiters.Count > 0)
					result = m_waiters.Dequeue();
				else
					++m_currentCount;
			}
			if (result != null)
				Task.Run(() => result.SetResult(true));
		}
	}

	/// <summary>
	/// Implementation of a lock construct that can be used with await statements,
	/// note that this lock is not re-entrant like the regular monitors used
	/// with the lock statement.
	/// </summary>
	public class AsyncLock
	{
		/// <summary>
		/// The semaphore that provides the general functionality of this lock
		/// </summary>
		private readonly AsyncSemaphore m_semaphore;

		/// <summary>
		/// The task used to encapsulate the lock
		/// </summary>
		private readonly Task<Releaser> m_releaser;

		/// <summary>
		/// Initializes a new instance of the <see cref="Ceen.AsyncLock"/> class.
		/// </summary>
		public AsyncLock()
		{
			m_semaphore = new AsyncSemaphore(1);
			m_releaser = Task.FromResult(new Releaser(this));
		}

		/// <summary>
		/// Aquires the exclusive lock, and awaits until it is available
		/// </summary>
		/// <returns>The async.</returns>
		public Task<Releaser> LockAsync()
		{
			var wait = m_semaphore.WaitAsync();

			return wait.IsCompleted ?
				m_releaser :
				wait.ContinueWith((_, state) => new Releaser((AsyncLock)state),
					this, CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
		}

		/// <summary>
		/// Internal releaser construct which needs to be disposed to unlock
		/// </summary>
		public struct Releaser : IDisposable
		{
			/// <summary>
			/// The parent instance
			/// </summary>
			private readonly AsyncLock m_parent;

			/// <summary>
			/// A value indicating if the lock can be disposed
			/// </summary>
			private bool m_canDispose;

			/// <summary>
			/// Initializes a new instance of the <see cref="Ceen.AsyncLock.Releaser"/> struct.
			/// </summary>
			/// <param name="parent">The parent lock.</param>
			internal Releaser(AsyncLock parent)
			{
				m_parent = parent;
				m_canDispose = true;
			}

			/// <summary>
			/// Releases all resource used by the <see cref="Ceen.AsyncLock.Releaser"/> object.
			/// </summary>
			/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Ceen.AsyncLock.Releaser"/>. The
			/// <see cref="Dispose"/> method leaves the <see cref="Ceen.AsyncLock.Releaser"/> in an unusable state. After
			/// calling <see cref="Dispose"/>, you must release all references to the <see cref="Ceen.AsyncLock.Releaser"/> so
			/// the garbage collector can reclaim the memory that the <see cref="Ceen.AsyncLock.Releaser"/> was occupying.</remarks>
			public void Dispose()
			{
				if (m_parent != null)
					lock (m_parent)
						if (m_canDispose)
						{
							m_parent.m_semaphore.Release();
							m_canDispose = false;
						}
			}
		}
	}
}
