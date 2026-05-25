using Eruru.Debouncer;

namespace Eruru.DebouncerSample {

	sealed internal class Program {

		static async Task Main () {
			// 创建防抖器
			// Create debouncer
			using var debouncer = new Debouncer<Context, string> (
				// 延迟时间
				// Delay interval
				TimeSpan.FromMilliseconds (500),
				// 自定义上下文
				// Custom context
				new Context (),
				// 异常处理
				// Exception handler
				static (debouncer, exception) => Console.WriteLine (exception)
			);
			while (true) {
#pragma warning disable CA1303 // 请不要将文本作为本地化参数传递
				Console.Write ("quickly input: ");
#pragma warning restore CA1303 // 请不要将文本作为本地化参数传递
				var text = await Console.In.ReadLineAsync ().ConfigureAwait (false);
				// 提交任务
				// Submit task
				debouncer.Post (static (debouncer, state) => {
					Console.WriteLine ();
					Console.WriteLine ($"response: {state}");
					if (debouncer.Context == null) {
						return Task.CompletedTask;
					}
					Interlocked.Increment (ref debouncer.Context.Counter);
					return Task.CompletedTask;
				}, text);
			}
		}

		sealed class Context {

			public int Counter;

		}

	}

}