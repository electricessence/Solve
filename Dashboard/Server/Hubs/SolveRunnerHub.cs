using Open.TaskManager;
using Open.TaskManager.Server;
using System.Threading.Tasks;

namespace Solve.Dashboard.Server.Hubs
{
	public class SolveRunnerHub : TaskRunnerHub
	{
		public const string Path = Constants.SolveRunnerHubPath;
		public SolveRunnerHub(ITaskRunnerRegistryService registry) : base(registry)
		{
		}

		public async ValueTask<int> Create(TaskRunnerOption option)
		{
			var r = await Registry.Create(option);
			return r.Id;
		}
	}
}
