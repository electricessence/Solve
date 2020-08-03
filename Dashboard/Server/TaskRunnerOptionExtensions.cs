using Open.TaskManager;
using Open.TaskManager.Server;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.Dashboard.Server
{
	public static class TaskRunnerRegistryServiceExtensions
	{
		public static TaskRunnerFactoryDelegate GetFactory(TaskRunnerOption option)
		{
			switch (option)
			{
				case TaskRunnerOption.Delay:
				{
					return async (id, token, progress) =>
					{
						var delay = Task.Delay(200000, token);
						progress("running");
						try
						{
							await delay;
						}
						catch (TaskCanceledException)
						{
							Debug.WriteLine("Count down cancelled.");
						}
						progress("stopping");
						await Task.Delay(2000);
					};
				}

				case TaskRunnerOption.Countdown:
				{
					return async (id, token, progress) =>
					{
						var start = Task.Delay(1000);
						progress("started");
						await start;
						for (var i = 5; i > 0; i--)
						{
							if (token.IsCancellationRequested) break;
							var delay = Task.Delay(1000, token);
							progress(i);
							try
							{
								await delay;
							}
							catch (TaskCanceledException)
							{
								Debug.WriteLine("Count down cancelled.");
							}
						}
						progress("stopping");
						await Task.Delay(2000);
					};

				}
			}

			throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown task option.");
		}

		public static ValueTask<ITaskRunner> Create(
			this ITaskRunnerRegistryService service,
			TaskRunnerOption option) => service.Create(GetFactory(option));

		public static ITaskRunnerFactory GetFactory(
			this ITaskRunnerRegistryService service,
			TaskRunnerOption option) => service.GetFactory(GetFactory(option));

		public static ITaskRunnerFactory<TIdentity> GetFactory<TIdentity>(
			this ITaskRunnerRegistryService service,
			TaskRunnerOption option) => service.GetFactory<TIdentity>(GetFactory(option));
	}
}
