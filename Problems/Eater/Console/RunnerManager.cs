using App.Metrics;
using Solve;
using System;
using System.Collections.Generic;
using System.Text;

namespace Eater.Console
{
	public class RunnerManager
	{
		private readonly object _sync = new object();
		private Runner? _runner;

		public bool IsRunning => _runner != null;

		public GenomeFactoryMetrics FactoryMetrics
			=> GenomeFactoryMetrics.Get(_runner?.MetricsSnapshot);

		public bool Start()
		{
			if (_runner != null) return false;
			lock (_sync)
			{
				var starting = _runner is null;
				if (starting)
					_runner = Runner.Start().runner;
				return starting;
			}
		}

		public bool Stop()
		{
			var r = _runner;
			if (r is null) return false;
			lock (_sync)
			{
				if (r != _runner) return false;

				r.Cancel();
				_runner = null;
				return true;
			}

		}

		public bool Toggle()
		{
			lock (_sync)
			{
				if (_runner is null)
				{
					_runner = Runner.Start().runner;
					return true;
				} else
				{
					_runner.Cancel();
					_runner = null;
					return false;
				}
			}
		}
	}
}
