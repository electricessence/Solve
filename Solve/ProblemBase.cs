using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class ProblemBase<TGenome> : IProblem<TGenome>
	where TGenome : class, IGenome
	{

		public RankedPool<TGenome> ChampionPool { get; }

		static int ProblemCount = 0;
		public int ID { get; } = Interlocked.Increment(ref ProblemCount);

		long _testCount = 0;
		public long TestCount => _testCount;


		protected ProblemBase(ushort championPoolSize = 0)
		{
			if (championPoolSize != 0)
				ChampionPool = new RankedPool<TGenome>(championPoolSize);
		}

		protected abstract double[] ProcessTestInternal(TGenome g, long sampleId);

		public double[] ProcessTest(TGenome g, long sampleId = 0)
		{
			try
			{
				return ProcessTestInternal(g, sampleId);
			}
			finally
			{
				Interlocked.Increment(ref _testCount);
			}
		}

		protected virtual Task<double[]> ProcessTestAsyncInternal(TGenome g, long sampleId)
			=> Task.FromResult(ProcessTestInternal(g, sampleId));

		public async Task<double[]> ProcessTestAsync(TGenome g, long sampleId = 0)
		{
			try
			{
				return await ProcessTestAsyncInternal(g, sampleId);
			}
			finally
			{
				Interlocked.Increment(ref _testCount);
			}
		}

		public abstract IReadOnlyList<string> FitnessLabels { get; }


	}
}
