﻿using System;
using System.Linq;

namespace Solve.ProcessingSchemes
{
	public sealed partial class TowerProcessingScheme<TGenome>
	{
		public sealed class ProblemTower : BroadcasterBase<(TGenome Genome, Fitness[])>, IGenomeProcessor<TGenome>
		{
			internal readonly TowerProcessingScheme<TGenome> Environment;
			internal readonly IProblem<TGenome> Problem;
			readonly Level Root;

			public ProblemTower(
				in IProblem<TGenome> problem,
				in TowerProcessingScheme<TGenome> environment) : base()
			{
				Problem = problem ?? throw new ArgumentNullException(nameof(problem));
				Environment = environment ?? throw new ArgumentNullException(nameof(environment));
				Root = new Level(0, this);
				this.Subscribe(champion => Environment.Broadcast((Problem, champion)));
			}

			public void Post(in TGenome next,
				in bool express,
				in bool expressToTop = false)
				=> Root.Post(
					(next, Problem.Pools.Select(f => new Fitness(f.Metrics)).ToArray()),
					express,
					expressToTop);

			public void Post(in TGenome next)
				=> Post(next, false);

		}
	}

}