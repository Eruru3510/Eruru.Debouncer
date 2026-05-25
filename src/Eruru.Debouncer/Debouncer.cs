using System.Diagnostics;

namespace Eruru.Debouncer {

#pragma warning disable CA1724 // 类型名与命名空间名称整体或部分冲突
	public class Debouncer<TContext, TState> : IDisposable {
#pragma warning restore CA1724 // 类型名与命名空间名称整体或部分冲突

		public TContext? Context { get; }

		readonly TimeSpan Time = TimeSpan.FromMilliseconds (500);
		readonly Timer Timer;
#if NET9_0_OR_GREATER
		readonly Lock Lock = new ();
#else
		readonly object Lock = new ();
#endif
		readonly Action<Debouncer<TContext, TState>, Exception>? OnException;
		Func<Debouncer<TContext, TState>, TState?, Task>? CallbackAsync;
		TState? State;
		int DisposeState;

		public Debouncer (
			TimeSpan? time = null, TContext? context = default,
			Action<Debouncer<TContext, TState>, Exception>? onException = null
		) {
			Time = time.GetValueOrDefault (Time);
			Context = context;
			OnException = onException;
			Timer = new (Timer_Elapsed, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		}

		protected virtual void Dispose (bool disposing) {
			if (Interlocked.Exchange (ref DisposeState, 1) != 0 || !disposing) {
				return;
			}
			Timer.Dispose ();
		}
		public void Dispose () {
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		public void Post (Func<Debouncer<TContext, TState>, TState?, Task> callbackAsync, TState? state = default) {
#if NET
			ArgumentNullException.ThrowIfNull (callbackAsync, nameof (callbackAsync));
#else
			if (callbackAsync == null) {
				throw new ArgumentNullException (nameof (callbackAsync));
			}
#endif
			CheckDisposed ();
			lock (Lock) {
				CallbackAsync = callbackAsync;
				State = state;
				Timer.Change (Time, Timeout.InfiniteTimeSpan);
			}
		}

		public void Cancel () {
			lock (Lock) {
				Timer.Change (Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
				CallbackAsync = null;
				State = default;
			}
		}

		void CheckDisposed () {
			if (Volatile.Read (ref DisposeState) == 0) {
				return;
			}
			throw new ObjectDisposedException (nameof (Debouncer<,>));
		}

		static void Timer_Elapsed (object? state) {
			if (state is not Debouncer<TContext, TState> debouncer) {
				return;
			}
			PerformCallback (debouncer, debouncer.State);
		}

		static void PerformCallback (Debouncer<TContext, TState> debouncer, TState? state) {
			_ = debouncer.CallbackAsync?.Invoke (debouncer, state).ContinueWith (
				static (task, state) => {
					if (!task.IsFaulted) {
						return;
					}
					if (state is not ValueTuple<Debouncer<TContext, TState>, TState> tuple) {
						return;
					}
					if (tuple.Item1.OnException == null) {
						Console.WriteLine (task.Exception);
						Debug.WriteLine (task.Exception);
						return;
					}
					tuple.Item1.OnException (tuple.Item1, task.Exception);
				},
				(debouncer, state),
				CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default
			);
		}

	}

}