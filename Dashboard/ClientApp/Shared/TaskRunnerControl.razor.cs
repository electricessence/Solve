using Microsoft.AspNetCore.Components;
using Open.TaskManager;
using System;
using System.Threading.Tasks;

namespace Solve.Dashboard.ClientApp.Shared
{
	public partial class TaskRunnerControlBase : SolveRunnerRegistryComponent
	{
		[Parameter]
		public int TaskId { get; set; }

		protected override async Task OnParametersSetAsync()
		{
			await base.OnParametersSetAsync();

			var taskId = TaskId;
			if (taskId <= 0) throw new InvalidOperationException("TaskId must be at least 1.");
			UpdateState(await Registry!.GetState(taskId));
			Progress = await Registry!.GetProgress(taskId);
		}


		protected TaskRunnerState StateTarget { get; private set; } = TaskRunnerState.Stopped;
		protected TaskRunnerState State { get; private set; } = TaskRunnerState.Unknown;
		private void UpdateState(TaskRunnerState state)
		{
			State = state;
			StateTarget = state;
		}
		protected string StateText => State.ToString() ?? string.Empty;
		protected bool IsStateInSync => State == StateTarget;

		protected object? Progress { get; private set; }
		protected string ProgressText => Progress?.ToString() ?? string.Empty;

		protected bool IsConnected => State switch
		{
			TaskRunnerState.Unknown => false,
			TaskRunnerState.Disposed => false,
			_ => true
		};

		protected bool IsRunning => State == TaskRunnerState.Running;
		protected bool IsStopping => State == TaskRunnerState.Stopping || StateTarget == TaskRunnerState.Stopping;

		protected async Task OnStart()
		{
			if (!IsStateInSync) return;
			StateTarget = TaskRunnerState.Running;
			StateHasChanged();
			try
			{
				await Registry!.Start(TaskId);
			}
			catch
			{
				StateTarget = State;
				throw;
			}
		}
		protected async Task OnStop()
		{
			if (!IsStateInSync) return;
			StateTarget = TaskRunnerState.Stopping;
			StateHasChanged();
			try
			{
				await Registry!.Stop(TaskId);
			}
			catch
			{
				StateTarget = State;
				throw;
			}
		}
		protected Task OnToggle(bool start) => start ? OnStart() : OnStop();

		protected override void OnStateUpdated(int id, TaskRunnerState state)
		{
			if (TaskId != id) return;
			UpdateState(state);
			base.OnStateUpdated(id, state);
		}

		protected override void OnProgressUpdated(int id, object? progress)
		{
			if (TaskId != id) return;
			Progress = progress;
			base.OnProgressUpdated(id, progress);
		}
	}
}
