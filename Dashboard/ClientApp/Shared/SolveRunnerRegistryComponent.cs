using Microsoft.AspNetCore.Components;
using Open.TaskManager;
using Solve.Dashboard.Client;
using System;

namespace Solve.Dashboard.ClientApp.Shared
{
	public abstract class SolveRunnerRegistryComponent : ComponentBase, IDisposable
	{
		private bool disposedValue;

		[Inject]
		protected SolveRunnerRegistryProxy? Registry { get; set; }

		protected virtual void OnStateUpdated(int id, TaskRunnerState state)
		{
			StateHasChanged();
		}

		protected virtual void OnProgressUpdated(int id, object? progress)
		{
			StateHasChanged();
		}

		protected override void OnInitialized()
		{
			base.OnInitialized();
			var registry = Registry ?? throw new InvalidOperationException("Registry not injected.");
			registry.StateUpdated += OnStateUpdated;
			registry.ProgressUpdated += OnProgressUpdated;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					var registry = Registry;
					if (registry != null)
					{
						registry.StateUpdated -= OnStateUpdated;
						registry.ProgressUpdated -= OnProgressUpdated;
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~SolveRunnerRegistryComponent()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
