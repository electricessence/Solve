using Open.TaskManager;
using Solve.Dashboard.ClientApp.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Solve.Dashboard.ClientApp.Pages
{
	public partial class SolveRunnerControlBase : SolveRunnerRegistryComponent
	{
		protected readonly HashSet<int> ActiveIds = new HashSet<int>();

		protected int Option;

		protected async Task OnAdd()
		{
			ActiveIds.Add(await Registry!.Create((TaskRunnerOption)Option));
			StateHasChanged();
		}

		protected override async Task OnInitializedAsync()
		{
			await base.OnInitializedAsync();
			await foreach (var id in Registry!.GetTaskRunnerIds())
				ActiveIds.Add(id);
		}

		protected override void OnStateUpdated(int id, TaskRunnerState state)
		{
			ActiveIds.Add(id);
			base.OnStateUpdated(id, state);
		}
	}
}
