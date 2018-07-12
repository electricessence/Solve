using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Solve
{
	public abstract partial class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
		where TGenome : class, IGenome
	{

		protected ReducibleGenomeFactoryBase(IEnumerable<TGenome> seeds = null) : base(seeds)
		{
		}

		protected override TGenome Registration(TGenome target)
		{
			if (target == null) return null;
			Register(target, out TGenome result, t => t.Freeze());
			return result;
		}

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
			if (source != null)
			{
				var reduced = GetReduced(source);
				if (reduced != null && reduced != source && reduced.Hash != source.Hash)
					yield return reduced;
			}
		}

		protected TGenome GetReduced(TGenome source)
		{
			if (Reductions.TryGetValue(source, out TGenome r))
				return r;

			var result = GetReducedInternal(source);
			if (result == null) return source;

			return Reductions.GetValue(source, key => Registration(result));
		}
	}
}
