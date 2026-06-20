using Eruru.Debouncer;

namespace Eruru.DebouncerTests;

public class DebouncerTest {

	[Fact]
	public async Task Debouncer () {
		var context = new Context ();
		using var debouncer = new Debouncer<Context, object> (TimeSpan.FromMilliseconds (50), context);
		for (var i = 0; i < 100; i++) {
			debouncer.Post (static (debouncer, state) => {
				if (debouncer.Context == null) {
					return Task.CompletedTask;
				}
				Interlocked.Increment (ref debouncer.Context.Counter);
				return Task.CompletedTask;
			});
		}
		await Task.Delay (TimeSpan.FromMilliseconds (100), TestContext.Current.CancellationToken).ConfigureAwait (true);
		Assert.Equal (1, context.Counter);
	}

	[Fact]
	public async Task Cancel () {
		var context = new Context ();
		using var debouncer = new Debouncer<Context, object> (TimeSpan.FromMilliseconds (50), context);
		debouncer.Post (static (debouncer, state) => {
			if (debouncer.Context == null) {
				return Task.CompletedTask;
			}
			Interlocked.Increment (ref debouncer.Context.Counter);
			return Task.CompletedTask;
		});
		await Task.Delay (TimeSpan.FromMilliseconds (25), TestContext.Current.CancellationToken).ConfigureAwait (true);
		debouncer.Cancel ();
		await Task.Delay (TimeSpan.FromMilliseconds (75), TestContext.Current.CancellationToken).ConfigureAwait (true);
		Assert.Equal (0, context.Counter);
	}

	sealed class Context {

		public int Counter;

	}

}