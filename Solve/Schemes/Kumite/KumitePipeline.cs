using Open.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Solve.Schemes
{
	// AKA King of the Hill
	public sealed class KumitePipeline<TGenome> : EnvironmentBase<TGenome>, IDisposable
		where TGenome : class, IGenome
	{

		public KumitePipeline(IGenomeFactory<TGenome> genomeFactory) : base(genomeFactory)
		{
			Worker = CancellableTask.Init(Process);
		}

		readonly CancellableTask Worker;

		void Process()
		{
			var root = new KumiteGenomeSelector<TGenome>(() => new GenomeFitness<TGenome>(genomeF, Process);
			var pool = new HashSet<TGenome>();
			foreach (var pg in GetTopGenome().AsParallel())
			{

			}
		}

		Task Process(TGenome genome)
			=> Task.WhenAll(ProblemsInternal.Select(p => p.ProcessTest(genome, 0, true)));

		IEnumerable<(IProblem<TGenome> Problem, TGenome Genome)> GetTopGenome()
		{
			// Step 1: Generate new...
			foreach (var p in Problems)
			{
				var g = Factory.GenerateOne();
				p.ProcessTest(g);
				yield return (p, g);
			}



			// Step 2: Ask for new...
			foreach (var pg in GetTopGenome())
			{

			}
		}

		void Post((IProblem<TGenome> Problem, TGenome Genome) top)
		{
			TopGenome.Post(top);
		}

		readonly BroadcastBlock<(IProblem<TGenome> Problem, TGenome Genome)> TopGenome
			= new BroadcastBlock<(IProblem<TGenome> Problem, TGenome Genome)>(null);

		public override IObservable<(IProblem<TGenome> Problem, TGenome Genome)> AsObservable()
			=> TopGenome.AsObservable();

		#region IDisposable Support
		public void Dispose()
			=> Worker.Dispose();

		protected override Task StartInternal()
		{
			Worker.Start();
			return Worker;
		}
		#endregion
	}
}
