using EvaluationFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solve
{
    public abstract class ReducibleGenomeFactoryBase<TGenome> : GenomeFactoryBase<TGenome>
    where TGenome : class, IReducibleGenome<TGenome>
    {

        protected override TGenome Registration(TGenome target)
        {
            if (target == null) return null;
            Register(target, out target, hash =>
            {
                Debug.Assert(target.Hash == hash);
                //target.RegisterVariations(GenerateVariations(target));
                //target.RegisterMutations(Mutate(target));

                var reduced = target.AsReduced();
                if (reduced != target)
                {
                    // A little caution here. Some possible evil recursion?
                    var reducedRegistration = Registration(reduced);
                    if (reduced != reducedRegistration)
                        target.ReplaceReduced(reducedRegistration);

                    //reduced.RegisterExpansion(hash);
                }
                target.Freeze();
            });
            return target;
        }

        public override TGenome[] AttemptNewCrossover(TGenome a, TGenome b, byte maxAttempts = 3)
        {
            if (a == null || b == null) return null;

            // Avoid inbreeding. :P
            if (a == b || a.AsReduced().Hash==b.AsReduced().Hash) return null;

            return base.AttemptNewCrossover(a, b, maxAttempts);
        }

		public override IEnumerable<TGenome> Expand(TGenome genome)
		{
			yield return genome.AsReduced();
			foreach (var g in base.Expand(genome))
				yield return g;
		}

	}
}
