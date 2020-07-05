using Open.TaskManager;
using System.Threading.Tasks;

namespace Solve.Dashboard
{
	public interface ISolveRunnerRegistry : ITaskRunnerRegistry
	{
		ValueTask<int> Create(TaskRunnerOption option);
	}
}
