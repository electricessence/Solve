using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Solve.ProcessingSchemes.Tower
{
	// ReSharper disable once PossibleInfiniteInheritance
	public abstract partial class TowerProcessingSchemeBase<TGenome, TTower, TEnvironment> : EnvironmentBase<TGenome>
		where TGenome : class, IGenome
		where TTower : TowerProcessingSchemeBase<TGenome, TTower, TEnvironment>.TowerBase
		where TEnvironment : EnvironmentBase<TGenome>
	{
		protected TowerProcessingSchemeBase(
			IGenomeFactory<TGenome> genomeFactory)
			: base(genomeFactory)
		{

		}

		protected IEnumerable<TTower> ActiveTowers { get; private set; }

		protected abstract TTower CreateTower(IProblem<TGenome> problem);

		protected override Task StartInternal(CancellationToken token)
		{
			var towers = Problems.Select(CreateTower).ToList();
			//Towers = towers.AsReadOnly();
			ActiveTowers = towers.Where(t => !t.Problem.HasConverged);
			return StartProcessing(token);
		}

		protected abstract Task StartProcessing(CancellationToken token);
	}
}
