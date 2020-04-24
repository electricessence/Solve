using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower.Simple
{
	public partial class SimpleTowerProcessingScheme<TGenome> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
	{
		public SimpleTowerProcessingScheme(IGenomeFactory<TGenome> genomeFactory) : base(genomeFactory)
		{
		}

		Tower CreateTower(IProblem<TGenome> problem)
			=> new Tower(problem, this);

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(CreateTower).ToList();
			//Towers = towers.AsReadOnly();
			var activeTowers = towers.Where(t => !t.Problem.HasConverged);
			return Task.WhenAll(
				Enumerable
					.Repeat<Func<Task>>(
						StartInternalCore,
						Environment.ProcessorCount)
					.Select(s => s())
					.ToArray());

			async Task StartInternalCore()
			{
				while (!token.IsCancellationRequested)
				{
					var stillActive = false;
					foreach (var tower in activeTowers)
					{
						if (token.IsCancellationRequested)
							break;

						stillActive = true;

						await tower.Process();
					}

					if (!stillActive)
						break;
				}
			}
		}

	}
}
