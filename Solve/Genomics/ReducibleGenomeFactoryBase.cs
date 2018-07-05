using System.Collections.Generic;

namespace Solve
{
	public abstract class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
	where TGenome : class, IReducibleGenome<TGenome>
	{

		protected ReducibleGenomeFactoryBase(in IEnumerable<TGenome> seeds = null) : base(seeds)
		{
		}

		protected override TGenome Registration(in TGenome target)
		{
			if (target == null) return null;
			Register(target, out TGenome result, t =>
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
			return result;
		}

		public override TGenome[] AttemptNewCrossover(in TGenome a, in TGenome b, in byte maxAttempts = 3)
		{
			if (a == null || b == null) return null;

			// Avoid inbreeding. :P
			if (a == b || a.AsReduced().Hash == b.AsReduced().Hash) return null;

			return base.AttemptNewCrossover(a, b, maxAttempts);
		}
	}
}
