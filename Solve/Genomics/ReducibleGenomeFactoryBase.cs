using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Solve
{
	public abstract class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
		where TGenome : class, IGenome
	{

		protected ReducibleGenomeFactoryBase()
		{ }

		protected ReducibleGenomeFactoryBase(IEnumerable<TGenome> seeds) : base(seeds)
		{ }

		public override TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
		{
			if (a == null || b == null
				// Avoid inbreeding. :P
				|| a == b
				|| GetReduced(a).Hash == GetReduced(b).Hash) return null;

			return base.AttemptNewCrossover(a, b, maxAttempts);
		}

		/* NOTE:
		 * Reductions are done inside the factory since more complex problems may require special references to catalogs or other important classes that should not be retained by the genome itself.
		 * It also ensures that a genome is kept clean and not responsible for the operation. */

		readonly ConditionalWeakTable<TGenome, TGenome> Reductions = new ConditionalWeakTable<TGenome, TGenome>();

		protected virtual TGenome GetReducedInternal(TGenome source) => null;

		protected override IEnumerable<TGenome> GetVariationsInternal(TGenome source)
		{
			if (source == null) yield break;
			var reduced = GetReduced(source);
			if (reduced != null && reduced != source && reduced.Hash != source.Hash)
				yield return reduced;
		}

		protected TGenome GetReduced(TGenome source)
		{
			if (Reductions.TryGetValue(source, out var r))
				return r;

			var result = GetReducedInternal(source);
			return result == null ? source : Reductions.GetValue(source, key => Registration(result));
		}
	}
}
