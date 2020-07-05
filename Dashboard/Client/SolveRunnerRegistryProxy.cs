using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Open.TaskManager.Client;
using System.Threading.Tasks;

namespace Solve.Dashboard.Client
{
	public class SolveRunnerRegistryProxy : TaskRunnerRegistryProxy, ISolveRunnerRegistry
	{
		public SolveRunnerRegistryProxy(IHubConnectionFactory<SolveRunnerRegistryProxy> hubConnectionFactory, ILogger<SolveRunnerRegistryProxy> logger) : base(hubConnectionFactory, logger)
		{
		}

		public async ValueTask<int> Create(TaskRunnerOption option)
			=> await (await ActiveConnection).InvokeAsync<int>(nameof(Create), option);
	}
}
