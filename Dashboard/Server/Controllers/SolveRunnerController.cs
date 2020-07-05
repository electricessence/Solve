using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Open.TaskManager;

namespace Solve.Dashboard.Server.Controllers
{
	[ApiController]
	[Route("api/solve-runner")]
	public class SolveRunnerController
	{
		public SolveRunnerController(ITaskRunnerRegistryService registry)
		{
			_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		}

		private readonly ITaskRunnerRegistryService _registry;

		[HttpGet("create/{option}")]
		public async ValueTask<int> Create(TaskRunnerOption option)
		{
			var r = await _registry.Create(option);
			return r.Id;
		}

		[HttpGet("{id}/state")]
		public ValueTask<TaskRunnerState> GetState(int id)
			=> _registry.GetState(id);

		[HttpHead("{id}/start")]
		public ValueTask<bool> Start(int id)
			=> _registry.Start(id);

		[HttpHead("{id}/stop")]
		public ValueTask Stop(int id)
			=> _registry.Stop(id);

	}
}
