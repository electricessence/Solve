using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public abstract class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
	where TGenome : class, IReducibleGenome<TGenome>
	{

		protected override TGenome Registration(TGenome target)
		{
			if (target == null) return null;
			Register(target, out target, t =>
			{
				//target.RegisterVariations(GenerateVariations(target));
				//target.RegisterMutations(Mutate(target));

				var reduced = t.AsReduced();
				if (reduced != t)
				{
					// A little caution here. Some possible evil recursion?
					var reducedRegistration = Registration(reduced);
					if (reduced != reducedRegistration)
						t.ReplaceReduced(reducedRegistration);

					//reduced.RegisterExpansion(hash);
				}
				t.Freeze();
			});
			return target;
		}

		public override IEnumerable<TGenome> AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
		{
			if (a == null || b == null || a == b || a.AsReduced().Hash == b.AsReduced().Hash)
				return Enumerable.Empty<TGenome>();

			return base.AttemptNewCrossover(a, b, maxAttempts);
		}

		public override IEnumerable<TGenome> Expand(TGenome genome, IEnumerable<TGenome> others = null)
		{
			yield return AssertFrozen(genome.AsReduced());

			foreach (var g in base.Expand(genome, others))
				yield return g;
		}

	}
}
