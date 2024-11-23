using Solve;

namespace Eater.Console;

public class RunnerManager(ushort size)
{
	public ushort Size { get; } = size;
	private readonly System.Threading.Lock _sync = new();
	private Runner? _runner;

	public bool IsRunning => _runner is not null;

	public GenomeFactoryMetrics FactoryMetrics
		=> GenomeFactoryMetrics.Get(_runner?.MetricsSnapshot);

	public bool Start()
	{
		if (_runner is not null) return false;
		lock (_sync)
		{
			bool starting = _runner is null;
			if (starting)
				_runner = Runner.Start(Size).runner;
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
				_runner = Runner.Start(Size).runner;
				return true;
			}
			else
			{
				_runner.Cancel();
				_runner = null;
				return false;
			}
		}
	}
}
