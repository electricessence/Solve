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

		IEnumerable<Tower> ActiveTowers;

		Tower CreateTower(IProblem<TGenome> problem)
			=> new Tower(problem, this);

		protected override Task StartInternal(CancellationToken cancellationToken)
		{
			var towers = Problems.Select(CreateTower).ToList();
			//Towers = towers.AsReadOnly();
			ActiveTowers = towers.Where(t => !t.Problem.HasConverged);
			return StartInternalCore(cancellationToken);

			async Task StartInternalCore(CancellationToken token)
			{
				while(!token.IsCancellationRequested)
				{

				}
			}
		}

	}
}
