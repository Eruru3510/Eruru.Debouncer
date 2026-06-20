namespace Eruru.Debouncer;

public class Debouncer<TContext, TState> : IDisposable {

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
		CheckDisposed ();
		lock (Lock) {
			CheckDisposed ();
			CallbackAsync = callbackAsync;
			State = state;
			Timer.Change (Time, Timeout.InfiniteTimeSpan);
		}
	}

	public void Cancel () {
		CheckDisposed ();
		lock (Lock) {
			CheckDisposed ();
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
#if NET
		ArgumentNullException.ThrowIfNull (state, nameof (state));
#else
		if (state == null) {
			throw new ArgumentNullException (nameof (state));
		}
#endif
		var debouncer = (Debouncer<TContext, TState>)state;
		if (Volatile.Read (ref debouncer.DisposeState) != 0) {
			return;
		}
		try {
			_ = debouncer.CallbackAsync!.Invoke (debouncer, debouncer.State).ContinueWith (static (task, state) => {
#if NET
				ArgumentNullException.ThrowIfNull (state, nameof (state));
#else
				if (state == null) {
					throw new ArgumentNullException (nameof (state));
				}
#endif
				var debouncer = (Debouncer<TContext, TState>)state;
				debouncer.OnException?.Invoke (debouncer, task.Exception!);
			}, debouncer, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
#pragma warning disable CA1031 // 不捕获常规异常类型
		} catch (Exception exception) {
#pragma warning restore CA1031 // 不捕获常规异常类型
			debouncer.OnException?.Invoke (debouncer, exception);
		}
	}

}