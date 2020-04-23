
using Eater.Console;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Solve;
using System;

namespace Eater.WebConsole.Controllers
{
	public class GenomeFactoryMetricsController : LoggedControllerBase
	{
		public GenomeFactoryMetricsController(ILogger<GenomeFactoryMetricsController> logger, RunnerManager runnerManager) : base(logger)
		{
			RunnerManager = runnerManager ?? throw new ArgumentNullException(nameof(runnerManager));
		}

		public RunnerManager RunnerManager { get; }

		[HttpGet]
		public IGenomeFactoryMetrics Get() => RunnerManager.FactoryMetrics;
	}
}
